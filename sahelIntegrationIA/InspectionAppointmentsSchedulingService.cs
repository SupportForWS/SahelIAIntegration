using Dapper;
using eServices.APIs.UserApp.OldApplication.Models;
using eServices.Kernel.Core.Extensions;
using eServicesV2.Kernel.Core.Configurations;
using eServicesV2.Kernel.Core.Constants;
using eServicesV2.Kernel.Core.Exceptions;
using eServicesV2.Kernel.Core.Logging;
using eServicesV2.Kernel.Core.Persistence;
using eServicesV2.Kernel.Domain.Entities;
using eServicesV2.Kernel.Domain.Entities.IdentityEntities;
using eServicesV2.Kernel.Domain.Entities.InspectionAppointmentsEntities;
using eServicesV2.Kernel.Domain.Entities.KGACEntities;
using eServicesV2.Kernel.Domain.Entities.LookupEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using eServicesV2.Kernel.Domain.Enums;
using eServicesV2.Kernel.Domain.Enums.StatesEnum;
using eServicesV2.Kernel.Domain.Helpers;
using eServicesV2.Kernel.Infrastructure.Persistence.Constants;
using eServicesV2.Kernel.Service.EmailQueueServices;
using eServicesV2.Kernel.Service.IdentityServices.Models.IdentityService;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Configurations;
using sahelIntegrationIA.Models;
using System.Data;
using System.Data.SqlClient;
using EinspectionsZones = eServicesV2.Kernel.Domain.Entities.InspectionAppointmentsEntities.EinspectionsZones;

namespace sahelIntegrationIA
{
    public class InspectionAppointmentsSchedulingService
    {
        private readonly IRequestLogger _logger;
        private readonly eServicesContext _eServicesContext;
        private readonly IBaseConfiguration _configurations;
        private readonly IRequestLogger _requestLogger;
        private readonly IDapper _dapper;
        private readonly SahelConfigurations _sahelConfigs;
        private readonly EmailQueueService _emailService;
        private string _jobCycleId = Guid.NewGuid().ToString();
        private readonly int _coolDownPeriod;

        public InspectionAppointmentsSchedulingService(IRequestLogger logger,
            eServicesContext eServicesContext,
            IBaseConfiguration configuration,
            IRequestLogger requestLogger,
            IDapper dapper,
            SahelConfigurations sahelConfigs)
        {
            _logger = logger;
            this._eServicesContext = eServicesContext;
            _configurations = configuration;
            _requestLogger = requestLogger;
            _dapper = dapper;
            _sahelConfigs = sahelConfigs;
            _coolDownPeriod = _sahelConfigs.inspectionAppointmentsConfigurations.InspectionAppointmentsCoolDownInDays;

        }
        public async Task ProcessInspectionAppointmentsQueue()
        {
            try
            {
                var inspectionAppointmentsQueues = await GetPendingInspectionAppointmentsRequests();

                foreach (var q in inspectionAppointmentsQueues)
                {
                    var orgId = await GetOrganizationId(q.UserID);
                    if (_sahelConfigs.inspectionAppointmentsConfigurations.EnablePenaltyCheckingForInspectionAppointments)
                    {
                        var preventionModel = await CheckPreventedOrganization(orgId);
                        if (preventionModel != null && preventionModel.IsPrevented)
                        {
                            var abortedDeclarationVehiclesList = q.DeclarationVehiclesIds
                                .Split(",")
                                .Select(v => "-" + v)
                                .ToList();
                            var abortedDeclarationVehiclesString = string.Join(",", abortedDeclarationVehiclesList);
                            q.DeclarationVehiclesIds = string.Join(",", abortedDeclarationVehiclesString);
                            q.IsPorcessed = true;
                            _eServicesContext.Set<inspectionAppointmentsQueue>().Update(q);
                            await _eServicesContext.Set<MobileNotification>().AddAsync(new MobileNotification
                            {
                                DateCreated = DateTime.Now,
                                NotificationType = 158,
                                ReadStatus = "0",
                                ReffType = preventionModel.PreventionDate.Value.ToString("dd-MM-yyyy"),
                                UserId = q.UserID,
                                ReferenceId = 0
                            });
                            await _eServicesContext.SaveChangesAsync();
                            return;
                        }
                    }
                    if (q.DeclarationVehiclesIds.Split(",").Count() == 0)
                    {
                        _requestLogger.LogException(new BusinessRuleException($"no declaration vehicles - queueId ={q.id}"));
                        return;
                    }
                    var declarationVehicleId = q.DeclarationVehiclesIds.Split(",").
                                                Select(i => i).
                                                First();
                    var userdetails = await _eServicesContext.Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                                            .Where(u => u.UserId == q.UserID)
                                            .FirstOrDefaultAsync();
                    if (userdetails == null)
                    {
                        _requestLogger.LogException(new BusinessRuleException("no user details - queueId ={q.id}"));
                        return;
                    }
                    var zone = await _eServicesContext.Set<EinspectionsZones>().FirstOrDefaultAsync();
                    var terminal = await _eServicesContext.Set<eServicesV2.Kernel.Domain.Entities.InspectionAppointmentsEntities.Eterminals>().FirstOrDefaultAsync();
                    var declarationDetails = GetDeclarationDetailsForDeclarationVehicleId(declarationVehicleId);
                    var declarationVehicles = GetVehiclesListForDeclaration(q, declarationDetails);
                    declarationVehicles = await GetLastInspectionPointsForVehicles(declarationVehicles);
                    var inspectionAppointmentsModel = await PrepareInspectionAppointmentsModels(q, declarationVehicles);
                    var portDetails = await _eServicesContext.Set<PortListsForInspectionAppointment>()
                        .Where(p => p.PortId == inspectionAppointmentsModel.DeclarationPortId)
                        .FirstOrDefaultAsync();
                    inspectionAppointmentsModel.DeclarationTypeId = inspectionAppointmentsModel.DeclarationVehicles[0].DeclarationTypeId;
                    var holidays = await _eServicesContext.Set<InspectionAppointmentsHolidaysConfigurations>().
                                            Where(h => h.EndDate >= DateTime.Now)
                                            .ToListAsync();
                    var eserviceRequestsIds = new List<long>();
                    if (!inspectionAppointmentsModel.IsHourlySet)
                    {
                        var upcomingAppointments = await _eServicesContext.Set<InspectionAppointments>()
                            .Where(i =>
                                i.DeclarationPortId == inspectionAppointmentsModel.DeclarationPortId &&
                                i.InspectionDate > DateTime.Now.Date &&
                                i.InpsectionPortId == inspectionAppointmentsModel.InspectionPortId &&
                                i.StateId == (int)InspectionAppointmentStateEnum.Booked
                            )
                            .GroupBy(i => i.InspectionDate)
                            .Select(g => new
                            {
                                InspectionDate = g.Key,
                                InspectionCount = g.Count()
                            })
                            .ToListAsync();

                        List<int> triedRandomNumbers = new List<int>();
                        int minAddedDays = 1, maxAddedDays = 4;
                        int randomNumber = 0;
                        int portMaxCapacity = 0;
                        DateTime targetDropoffDateTime = DateTime.Now.AddDays(minAddedDays - 1)
                            .Date // Get the date only (midnight)
                            .AddHours(_sahelConfigs.inspectionAppointmentsConfigurations.VehicleDropOffWindowStart);

                        // Calculate the difference in hours between now and the drop-off start time
                        int dropoffWindowForNextDay = (int)(targetDropoffDateTime - DateTime.Now).TotalHours;

                        // Adjust minAddedDays based on OperationalHoursGap
                        if (dropoffWindowForNextDay < _sahelConfigs.inspectionAppointmentsConfigurations.OperationalHoursGap)
                        {
                            minAddedDays++;
                        }

                        while (inspectionAppointmentsModel.DeclarationVehicles.Any(v => v.InspectionDate is null))
                        {
                            if (triedRandomNumbers.Count < (maxAddedDays - minAddedDays))
                            {
                                Random random = new Random();
                                List<int> allowedNumbers = Enumerable.Range(minAddedDays, maxAddedDays - minAddedDays)
                                     .Where(n => !triedRandomNumbers.Contains(n))
                                     .ToList();
                                randomNumber = allowedNumbers[random.Next(allowedNumbers.Count)];

                                triedRandomNumbers.Add(randomNumber);
                            }
                            else
                            {
                                randomNumber = (randomNumber != maxAddedDays && randomNumber < maxAddedDays) ? maxAddedDays : ++randomNumber;
                            }


                            var pickedDate = DateTime.Now.AddDays(randomNumber).Date;
                            if (holidays.Any(h => h.StartDate <= pickedDate && h.EndDate >= pickedDate) || pickedDate.DayOfWeek == DayOfWeek.Friday)
                            {
                                continue;
                            }

                            var randomDate = upcomingAppointments.Find(d => d.InspectionDate == pickedDate);
                            var portExceptionlCapacity = await _eServicesContext.Set<ExceptionalPortsInspectionCapacityConfiguration>()
                                               .Where(c =>
                                                        c.portId == inspectionAppointmentsModel.DeclarationPortId &&
                                                        c.StartDate <= pickedDate && c.EndDate >= pickedDate)
                                               .Select(c => c.Capacity)
                                               .FirstOrDefaultAsync();
                            portMaxCapacity = portExceptionlCapacity != 0 ? portExceptionlCapacity : inspectionAppointmentsModel.PortMaximumCapacity;
                            var availableDateSlots = randomDate != null ? portMaxCapacity - randomDate.InspectionCount
                                                                     : portMaxCapacity;
                            if (availableDateSlots == 0)
                            {
                                continue;
                            }
                            if (availableDateSlots >= inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList().Count)
                            {
                                inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList().ForEach(v =>
                                {
                                    if (!portDetails.CooldownPeriodCommitted)
                                    {
                                        v.InspectionDate = pickedDate;
                                    }

                                    if (portDetails.CooldownPeriodCommitted &&
                                       (v.LastInspectionDate is null || Math.Abs((pickedDate - v.LastInspectionDate.Value).Days) > _coolDownPeriod))
                                    {
                                        v.InspectionDate = pickedDate;
                                    }
                                });
                            }
                            else if (availableDateSlots > 0 && availableDateSlots < inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList().Count)
                            {
                                var testedVehicles = new List<string>();
                                for (int i = 0; i < availableDateSlots; i++)
                                {
                                    var vehicle = inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && !testedVehicles.Contains(v.VehicleId)).ToList()[i];
                                    if (!portDetails.CooldownPeriodCommitted)
                                    {
                                        vehicle.InspectionDate = pickedDate;
                                    }
                                    else
                                    {
                                        if(vehicle.LastInspectionDate is null || Math.Abs((pickedDate - vehicle.LastInspectionDate.Value).Days) > _coolDownPeriod) 
                                        {
                                            vehicle.InspectionDate = pickedDate;
                                        }
                                    }

                                    if(vehicle.InspectionDate is null)
                                    {
                                        testedVehicles.Add(vehicle.VehicleId);
                                        i--;
                                    }

                                    if(testedVehicles.Count == inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList().Count)
                                    {
                                        break;
                                    }
/*                                    if (portDetails.CooldownPeriodCommitted &&
                                           (inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList()[i].LastInspectionDate is null
                                            ||
                                           Math.Abs((pickedDate - inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList()[i].LastInspectionDate.Value).Days) > _coolDownPeriod))
                                    {
                                        inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null).ToList()[i].InspectionDate = pickedDate;
                                    }*/
                                }
                            }

                        }
                        string type = inspectionAppointmentsModel.DeclarationTypeId == (int)BillTypesEnums.Export ? "E" : "I";
                        DeclarationPortEnum port = (DeclarationPortEnum)Enum.Parse(typeof(DeclarationPortEnum), inspectionAppointmentsModel.DeclarationPortId.ToString());
                        string portDesc = port.GetEnumDescription();
                        var scheduledInspectionRequests = SplitByInspectionDate(inspectionAppointmentsModel);

                        foreach (var appointment in scheduledInspectionRequests)
                        {
                            var bookedAppointments = await InititalReservceAppointments(date: (DateTime)appointment.InspectionDate,
                                time: null,
                                isHourly: appointment.IsHourlySet,
                                vehicles: appointment.DeclarationVehicles.Select(v => v.VehicleId).ToList(),
                                declarationNumber: appointment.DeclarationNumber,
                                inspectionPortStartRampNumber: appointment.StartRampNumber,
                                inspectionPortEndRampNumber: appointment.EndRampNumber,
                                inspectionPortCapacity: appointment.PortCapacityPerRamp,
                                inspectionPortSequanceType: appointment.SequenceType,
                                inspectionPortId: appointment.InspectionPortId,
                                declarationPortId: appointment.DeclarationPortId,
                                portCode: appointment.LocationCode,
                                portMaxCapacity: portMaxCapacity);

                            if (bookedAppointments.Count == 0)
                            {
                                _requestLogger.LogException(new BusinessRuleException($"bookedAppointments = 0 - queueId ={q.id}"));
                                return;
                            }
                            var requestData = await GenerateIdForInspectionAppointment(type, portDesc);

                            ServiceRequest serviceRequest = new ServiceRequest
                            (
                                eserviceRequestId: requestData[0].RequestId,
                                eserviceRequestNumber: requestData[0].RequestNumber,
                                createdBy: appointment.UserId.ToString(),
                                stateId: nameof(ServiceRequestStatesEnum.EServiceRequestSubmittedState),
                                serviceId: (int)ServiceTypesEnum.InspectionAppointments,
                                requesterUserId: appointment.UserId
                            );

                            await _eServicesContext.Set<ServiceRequest>().AddAsync(serviceRequest);

                            var requestDtaislsIds = await GenerateRequesDetailsId();
                            ServiceRequestsDetail serviceRequestDetails = new ServiceRequestsDetail(
                                eserviceRequestDetailsId: requestDtaislsIds[0],
                                eserviceRequestId: serviceRequest.EserviceRequestId,
                                requestForUserType: appointment.UserId,
                                requestServicesId: (int)ServiceTypesEnum.InspectionAppointments,
                                stateId: nameof(ServiceRequestDetailsStatesEnum.EServicesRequestDetailsSubmittedState),
                                organizationId: declarationDetails.ConsigneeOrgId,
                                createdBy: appointment.UserId.ToString(),
                                requesterUserId: appointment.UserId,
                                requestForUserId: appointment.UserId, //TODO: Check
                                portId: appointment.DeclarationPortId,
                                zoneId: zone.ZoneId,
                                terminalId: terminal.TerminalId,
                                declarationNumber: appointment.DeclarationNumber,
                                inspectionAppointmentDate: appointment.InspectionDate,
                                declarationTypeId: appointment.DeclarationTypeId,
                                declarationId: appointment.DeclarationId);

                            await _eServicesContext.Set<ServiceRequestsDetail>().AddAsync(serviceRequestDetails);

                            var appointments = new List<InspectionAppointments>();
                            List<int> bookedRamsForCurrnetRequest = new();
                            foreach (var vehicle in appointment.DeclarationVehicles)
                            {
                                var res = bookedAppointments.First(a => a.VehicleId == vehicle.VehicleId);
                                InspectionAppointments inspection = new InspectionAppointments();
                                inspection.DeclarationVehicleId = Convert.ToInt32(vehicle.VehicleId);
                                inspection.CreatedBy = appointment.UserId;
                                inspection.DateCreated = DateTime.Now;
                                inspection.EserviceRequestId = requestData[0].RequestId;
                                inspection.StateId = (int)InspectionAppointmentStateEnum.Booked;
                                inspection.Printed = false;
                                inspection.OrganizationId = declarationDetails.ConsigneeOrgId;
                                inspection.InspectionRampNumber = res.RampNumber;
                                inspection.InspectionToken = res.InspectionToken;
                                inspection.InspectionTokenDate = res.InspectionTokenDate;
                                inspection.InspectionTokenCounter = res.InspectionTokenCounter;
                                inspection.DeclarationNumber = declarationDetails.DeclarationNumber;
                                inspection.DeclarationId = appointment.DeclarationId;
                                inspection.DeclarationPortId = appointment.DeclarationPortId;
                                inspection.InpsectionPortId = res.InpsectionPortId;
                                inspection.InspectionDate = res.InspectionDate;
                                inspection.InspectionTime = res.InspectionTime;
                                inspection.DriverCivilId = vehicle.DriverCivilId;
                                inspection.DriverPassportNumber = vehicle.DriverPassportNumber;
                                inspection.VehiclePlateNumber = vehicle.PlateNo;
                                inspection.Country = vehicle.Country;

                                bookedRamsForCurrnetRequest.Add((int)inspection.InspectionRampNumber);
                                appointments.Add(inspection);

                                await _eServicesContext.Set<InspectionAppointments>().AddAsync(inspection);
                            }
                            await _eServicesContext.Set<KGACEmailOutSyncQueue>().AddAsync(new KGACEmailOutSyncQueue
                            {
                                UserId = userdetails.UserId.ToString(),
                                TOEmailAddress = userdetails.EmailId,
                                MsgType = "BPSubmit",
                                MailPriority = "Normal",
                                Status = "Created",
                                Sync = 0,
                                SampleRequestNo = requestData[0].RequestId.ToString()
                            });
                            eserviceRequestsIds.Add(serviceRequest.EserviceRequestId);
                        }
                        q.IsPorcessed = true;
                        q.EservicerequestIds = string.Join(",", eserviceRequestsIds);
                        _eServicesContext.Set<inspectionAppointmentsQueue>().Update(q);
                        await _eServicesContext.Set<MobileNotification>().AddAsync(new MobileNotification
                        {
                            DateCreated = DateTime.Now,
                            NotificationType = 157,
                            ReadStatus = "0",
                            ReffType = q.EservicerequestIds,
                            UserId = q.UserID,
                            ReferenceId = Convert.ToInt32(eserviceRequestsIds.First())
                        });
                        await _eServicesContext.SaveChangesAsync();
                    }
                    else
                    {
                        portDetails = await _eServicesContext.Set<PortListsForInspectionAppointment>()
                                            .Where(p => p.PortId == inspectionAppointmentsModel.DeclarationPortId)
                                            .FirstOrDefaultAsync();
                        var upcomingAppointments = await _eServicesContext.Set<InspectionAppointments>()
                            .Where(i =>
                                i.DeclarationPortId == inspectionAppointmentsModel.DeclarationPortId &&
                                i.InspectionDate > DateTime.Now.Date &&
                                i.InpsectionPortId == inspectionAppointmentsModel.InspectionPortId
                            )
                            .GroupBy(i => new
                            {
                                i.InspectionDate,
                                InspectionTime = i.InspectionTime
                            })
                            .Select(g => new
                            {
                                InspectionDate = g.Key.InspectionDate,
                                InspectionTime = g.Key.InspectionTime,
                                InspectionCount = g.Count()
                            })
                            .ToListAsync();

                        List<DateTime> randomizedDateTimes = new List<DateTime>();
                        List<DateTime> triedDateTimes = new List<DateTime>();
                        int minAddedDays = 1, maxAddedDays = 4;
                        TimeSpan? fromTime = portDetails.FromTime, toTime = portDetails.ToTime;
                        DateTime randomDateTime = DateTime.Now;
                        int portMaxCapacity = 0;
                        DateTime targetDropoffDateTime = DateTime.Now.AddDays(minAddedDays - 1)
                            .Date // Get the date only (midnight)
                            .AddHours(_sahelConfigs.inspectionAppointmentsConfigurations.VehicleDropOffWindowStart);

                        // Calculate the difference in hours between now and the drop-off start time
                        int dropoffWindowForNextDay = (int)(targetDropoffDateTime - DateTime.Now).TotalHours;

                        // Adjust minAddedDays based on OperationalHoursGap
                        if (dropoffWindowForNextDay < _sahelConfigs.inspectionAppointmentsConfigurations.OperationalHoursGap)
                        {
                            minAddedDays++;
                        }
                        for (DateTime dt = DateTime.Now.AddDays(minAddedDays); dt <= DateTime.Now.AddDays(maxAddedDays - 1); dt = dt.AddDays(1))
                        {
                            for (int hour = fromTime.Value.Hours; hour <= toTime.Value.Hours; hour++)
                            {
                                randomizedDateTimes.Add(new DateTime(dt.Year, dt.Month, dt.Day, hour, 0, 0));
                            }
                        }
                        while (inspectionAppointmentsModel.DeclarationVehicles.Any(v => v.InspectionDate is null || v.InspectionTime is null))
                        {

                            if (triedDateTimes.Count < randomizedDateTimes.Count)
                            {
                                List<DateTime> availableDateTimes = randomizedDateTimes
                                                        .Where(dt => !triedDateTimes.Contains(dt))
                                                        .ToList();
                                Random random = new Random();
                                randomDateTime = availableDateTimes[random.Next(availableDateTimes.Count)];
                                triedDateTimes.Add(randomDateTime);
                            }
                            else
                            {
                                var maxTriedDate = randomizedDateTimes.Max();
                                if (maxTriedDate.TimeOfDay == toTime)
                                {
                                    randomDateTime = randomDateTime.AddDays(1);
                                    triedDateTimes.Add(randomDateTime);
                                }
                                else if (maxTriedDate.TimeOfDay < toTime && maxTriedDate.TimeOfDay >= fromTime)
                                {
                                    randomDateTime = randomDateTime.AddHours(1);
                                    triedDateTimes.Add(randomDateTime);
                                }
                            }

                            var pickedDate = randomDateTime;
                            if (holidays.Any(h => h.StartDate <= pickedDate.Date && h.EndDate >= pickedDate.Date) || pickedDate.DayOfWeek == DayOfWeek.Friday)
                            {
                                continue;
                            }

                            var randomDate = upcomingAppointments.Find(d => d.InspectionDate == pickedDate.Date && d.InspectionTime == pickedDate.TimeOfDay);
                            var portExceptionlCapacity = await _eServicesContext.Set<ExceptionalPortsInspectionCapacityConfiguration>()
                                               .Where(c =>
                                                        c.portId == inspectionAppointmentsModel.DeclarationPortId &&
                                                        c.StartDate <= pickedDate && c.EndDate >= pickedDate)
                                               .Select(c => c.Capacity)
                                               .FirstOrDefaultAsync();
                            portMaxCapacity = portExceptionlCapacity != 0 ? portExceptionlCapacity : inspectionAppointmentsModel.PortMaximumCapacity;
                            var availableDateSlots = randomDate != null ? portMaxCapacity - randomDate.InspectionCount
                                                                     : portMaxCapacity;

                            if (availableDateSlots >= inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList().Count)
                            {
                                inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList().ForEach(v =>
                                {
                                    
                                    if (!portDetails.CooldownPeriodCommitted || (portDetails.CooldownPeriodCommitted &&
                                        (v.LastInspectionDate is null || Math.Abs((pickedDate - v.LastInspectionDate.Value).Days) > _coolDownPeriod)))
                                    {
                                        v.InspectionDate = pickedDate.Date;
                                        v.InspectionTime = pickedDate.TimeOfDay;
                                    }
                                });
                            }
                            else if (availableDateSlots > 0 && availableDateSlots < inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList().Count)
                            {
                                var testedVehicles = new List<string>();
                                for (int i = 0; i < availableDateSlots; i++)
                                {
                                    var vehicle = inspectionAppointmentsModel.DeclarationVehicles
                                        .Where(v => v.InspectionDate is null && v.InspectionTime is null && !testedVehicles.Contains(v.VehicleId))
                                        .ToList()[i];
                                    if (!portDetails.CooldownPeriodCommitted)
                                    {
                                        var inspectedVehicle = inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList()[i];
                                        inspectedVehicle.InspectionDate = pickedDate.Date;
                                        inspectedVehicle.InspectionTime = pickedDate.TimeOfDay;
                                    }

                                    if (portDetails.CooldownPeriodCommitted &&
                                        (vehicle.LastInspectionDate is null ||
                                            Math.Abs((pickedDate - vehicle.LastInspectionDate.Value).Days) > _coolDownPeriod))
                                    {
                                        var inspectedVehicle = inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList()[i];
                                        inspectedVehicle.InspectionDate = pickedDate.Date;
                                        inspectedVehicle.InspectionTime = pickedDate.TimeOfDay;
                                    }

                                    if(vehicle.InspectionDate is null || vehicle.InspectionTime is null)
                                    {
                                        testedVehicles.Add(vehicle.VehicleId);
                                        i--;
                                    }

                                    if (testedVehicles.Count == inspectionAppointmentsModel.DeclarationVehicles.Where(v => v.InspectionDate is null && v.InspectionTime is null).ToList().Count)
                                    {
                                        break;
                                    }
                                }
                            }

                        }
                        string type = inspectionAppointmentsModel.DeclarationTypeId == (int)BillTypesEnums.Export ? "E" : "I";
                        DeclarationPortEnum port = (DeclarationPortEnum)Enum.Parse(typeof(DeclarationPortEnum), inspectionAppointmentsModel.DeclarationPortId.ToString());
                        string portDesc = port.GetEnumDescription();
                        var scheduledInspectionRequests = SplitByInspectionDateAndTime(inspectionAppointmentsModel);

                        foreach (var appointment in scheduledInspectionRequests)
                        {
                            var bookedAppointments = await InititalReservceAppointments(date: (DateTime)appointment.InspectionDate.Value.Date,
                                time: appointment.InspectionTime,
                                isHourly: appointment.IsHourlySet,
                                vehicles: appointment.DeclarationVehicles.Select(v => v.VehicleId).ToList(),
                                declarationNumber: appointment.DeclarationNumber,
                                inspectionPortStartRampNumber: appointment.StartRampNumber,
                                inspectionPortEndRampNumber: appointment.EndRampNumber,
                                inspectionPortCapacity: appointment.PortCapacityPerRamp,
                                inspectionPortSequanceType: appointment.SequenceType,
                                inspectionPortId: appointment.InspectionPortId,
                                declarationPortId: appointment.DeclarationPortId,
                                portCode: appointment.LocationCode,
                                portMaxCapacity: portMaxCapacity);

                            if (bookedAppointments.Count == 0)
                            {
                                _requestLogger.LogException(new BusinessRuleException($"bookedAppointments = 0 - queueId ={q.id}"));
                                return;
                            }
                            var requestData = await GenerateIdForInspectionAppointment(type, portDesc);

                            ServiceRequest serviceRequest = new ServiceRequest
                            (
                                eserviceRequestId: requestData[0].RequestId,
                                eserviceRequestNumber: requestData[0].RequestNumber,
                                createdBy: appointment.UserId.ToString(),
                                stateId: nameof(ServiceRequestStatesEnum.EServiceRequestSubmittedState),
                                serviceId: (int)ServiceTypesEnum.InspectionAppointments,
                                requesterUserId: appointment.UserId
                            );

                            await _eServicesContext.Set<ServiceRequest>().AddAsync(serviceRequest);

                            var requestDtaislsIds = await GenerateRequesDetailsId();
                            ServiceRequestsDetail serviceRequestDetails = new ServiceRequestsDetail(
                                eserviceRequestDetailsId: requestDtaislsIds[0],
                                eserviceRequestId: serviceRequest.EserviceRequestId,
                                requestForUserType: appointment.UserId,
                                requestServicesId: (int)ServiceTypesEnum.InspectionAppointments,
                                stateId: nameof(ServiceRequestDetailsStatesEnum.EServicesRequestDetailsSubmittedState),
                                organizationId: declarationDetails.ConsigneeOrgId,
                                createdBy: appointment.UserId.ToString(),
                                requesterUserId: appointment.UserId,
                                requestForUserId: appointment.UserId, //TODO: Check
                                portId: appointment.DeclarationPortId,
                                zoneId: zone.ZoneId,
                                terminalId: terminal.TerminalId,
                                declarationNumber: appointment.DeclarationNumber,
                                inspectionAppointmentDate: appointment.InspectionDate,
                                declarationTypeId: appointment.DeclarationTypeId,
                                declarationId: appointment.DeclarationId);

                            await _eServicesContext.Set<ServiceRequestsDetail>().AddAsync(serviceRequestDetails);

                            var appointments = new List<InspectionAppointments>();
                            List<int> bookedRamsForCurrnetRequest = new();
                            foreach (var vehicle in appointment.DeclarationVehicles)
                            {
                                var res = bookedAppointments.First(a => a.VehicleId == vehicle.VehicleId);
                                InspectionAppointments inspection = new InspectionAppointments();
                                inspection.DeclarationVehicleId = Convert.ToInt32(vehicle.VehicleId);
                                inspection.CreatedBy = appointment.UserId;
                                inspection.DateCreated = DateTime.Now;
                                inspection.EserviceRequestId = requestData[0].RequestId;
                                inspection.StateId = (int)InspectionAppointmentStateEnum.Booked;
                                inspection.Printed = false;
                                inspection.OrganizationId = declarationDetails.ConsigneeOrgId;
                                inspection.InspectionRampNumber = res.RampNumber;
                                inspection.InspectionToken = res.InspectionToken;
                                inspection.InspectionTokenDate = res.InspectionTokenDate;
                                inspection.InspectionTokenCounter = res.InspectionTokenCounter;
                                inspection.DeclarationNumber = declarationDetails.DeclarationNumber;
                                inspection.DeclarationId = appointment.DeclarationId;
                                inspection.DeclarationPortId = appointment.DeclarationPortId;
                                inspection.InpsectionPortId = res.InpsectionPortId;
                                inspection.InspectionDate = res.InspectionDate;
                                inspection.InspectionTime = res.InspectionTime;
                                inspection.DriverCivilId = vehicle.DriverCivilId;
                                inspection.DriverPassportNumber = vehicle.DriverPassportNumber;
                                inspection.VehiclePlateNumber = vehicle.PlateNo;
                                inspection.Country = vehicle.Country;

                                bookedRamsForCurrnetRequest.Add((int)inspection.InspectionRampNumber);
                                appointments.Add(inspection);

                                await _eServicesContext.Set<InspectionAppointments>().AddAsync(inspection);

                            }

                            await _eServicesContext.Set<KGACEmailOutSyncQueue>().AddAsync(new KGACEmailOutSyncQueue
                            {
                                UserId = userdetails.UserId.ToString(),
                                TOEmailAddress = userdetails.EmailId,
                                MsgType = "BPSubmit",
                                MailPriority = "Normal",
                                Status = "Created",
                                Sync = 0,
                                SampleRequestNo = requestData[0].RequestId.ToString()
                            });
                            eserviceRequestsIds.Add(serviceRequest.EserviceRequestId);
                        }
                        q.IsPorcessed = true;
                        q.EservicerequestIds = string.Join(",", eserviceRequestsIds);
                        _eServicesContext.Set<inspectionAppointmentsQueue>().Update(q);
                        await _eServicesContext.Set<MobileNotification>().AddAsync(new MobileNotification
                        {
                            DateCreated = DateTime.Now,
                            NotificationType = 157,
                            ReadStatus = "0",
                            ReffType = q.EservicerequestIds,
                            UserId = q.UserID,
                            ReferenceId = Convert.ToInt32(eserviceRequestsIds.First())
                        });

                        if (q.RequestSource == "Sahel")
                        {
                            //TODO: Add notification for Sahel
                        }
                        await _eServicesContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _requestLogger.LogException(ex,
                    $"{_jobCycleId} - inspection appointment scheduling - {0,1}",
                    new object[] { ex.Message, ex.InnerException?.Message });
            }
        }
        List<InspectionAppPreparationModel> SplitByInspectionDateAndTime(InspectionAppPreparationModel model)
        {
            return model.DeclarationVehicles
                        .GroupBy(v => new { v.InspectionDate, v.InspectionTime }) // Group by both Date & Time
                        .OrderBy(v => v.Key.InspectionDate)
                        .ThenBy(v => v.Key.InspectionTime)
                        .Select(group => new InspectionAppPreparationModel
                        {
                            // Copy other properties
                            UserId = model.UserId,
                            DeclarationId = model.DeclarationId,
                            DateCreated = model.DateCreated,
                            DeclarationNumber = model.DeclarationNumber,
                            DeclarationPortId = model.DeclarationPortId,
                            InspectionPortId = model.InspectionPortId,
                            PortMaximumCapacity = model.PortMaximumCapacity,
                            PortCapacityPerRamp = model.PortCapacityPerRamp,
                            StartRampNumber = model.StartRampNumber,
                            EndRampNumber = model.EndRampNumber,
                            LocationCode = model.LocationCode,
                            SequenceType = model.SequenceType,
                            OrgId = model.OrgId,
                            IsHourlySet = model.IsHourlySet,
                            DeclarationTypeId = model.DeclarationTypeId,

                            // Assign grouped vehicles
                            DeclarationVehicles = group.ToList(),

                            // Assign group key values
                            InspectionDate = group.Key.InspectionDate,
                            InspectionTime = group.Key.InspectionTime
                        })
                        .ToList();
        }

        List<InspectionAppPreparationModel> SplitByInspectionDate(InspectionAppPreparationModel model)
        {
            return model.DeclarationVehicles
                        .GroupBy(v => v.InspectionDate)
                        .OrderBy(v=>v.Key)
                        .Select(group => new InspectionAppPreparationModel
                        {
                            UserId = model.UserId,
                            DeclarationId = model.DeclarationId,
                            DateCreated = model.DateCreated,
                            DeclarationNumber = model.DeclarationNumber,
                            DeclarationPortId = model.DeclarationPortId,
                            InspectionPortId = model.InspectionPortId,
                            PortMaximumCapacity = model.PortMaximumCapacity,
                            PortCapacityPerRamp = model.PortCapacityPerRamp,
                            StartRampNumber = model.StartRampNumber,
                            EndRampNumber = model.EndRampNumber,
                            LocationCode = model.LocationCode,
                            SequenceType = model.SequenceType,
                            OrgId = model.OrgId,
                            IsHourlySet = model.IsHourlySet,
                            DeclarationTypeId = model.DeclarationTypeId,
                            DeclarationVehicles = group.ToList(),
                            InspectionDate = group.Key
                        })
                        .ToList();
        }
        public async Task<List<inspectionAppointmentsQueue>> GetPendingInspectionAppointmentsRequests()
        {
            return await _eServicesContext
                                    .Set<inspectionAppointmentsQueue>()
                                    .Where(I => !I.IsPorcessed)
                                    .OrderBy(I => I.DateCreated)
                                    .ToListAsync();
        }
        private VehiclesDTO GetDeclarationDetailsForDeclarationVehicleId(string decVehId)
        {
            var decVehicleId = Convert.ToInt32(decVehId);
            var data = GetVehiclesDataByDecVehicleId(decVehicleId);

            VehiclesDTO vehicle = new VehiclesDTO();
            if (data.Tables != null && data.Tables?.Count > 0 && data.Tables["VehicleData"].Rows.Count > 0)
            {
                vehicle = data.Tables[0].AsEnumerable().Select(v => new VehiclesDTO
                {
                    DeclarationNumber = v["DeclarationNumber"].ToString(),
                    DeclarationId = v["DeclarationId"].ToString(),
                    ConsigneeOrgId = Convert.ToInt32(v["ConsigneeOrgId"]),
                    PortId = Convert.ToInt32(v["Portid"])

                }).FirstOrDefault();

                return vehicle;
            }
            _requestLogger.LogException(new Exception("GetVehiclesDataByDecVehicleId - no declaration details"));
            return null;
        }
        public DataSet GetVehiclesDataByDecVehicleId(int decVehicleId)
        {
            DataSet dataSet = new DataSet();
            try
            {
                using SqlConnection connection = new SqlConnection(_configurations.ConnectionStrings.Default);
                using SqlCommand sqlCommand = new SqlCommand("etrade.GetVehicleDataUsingDecVehicleId", connection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.Add("@DecVehId", SqlDbType.VarChar).Value = decVehicleId;
                new SqlDataAdapter(sqlCommand).Fill(dataSet);
                for (int i = 0; i < dataSet.Tables.Count; i++)
                {
                    if (dataSet.Tables[i].Columns.Contains("TableName"))
                    {
                        if (dataSet.Tables[i].Rows.Count > 0)
                        {
                            dataSet.Tables[i].TableName = dataSet.Tables[i].Rows[0]["TableName"].ToString();
                        }
                    }
                    else
                    {
                        dataSet.Tables.RemoveAt(i);
                        i--;
                    }
                }

                return dataSet;
            }
            catch (Exception ex)
            {
                CommonFunctions.LogUserActivity("GetVehicleDataUsingDecVehicleId", "", "", "", "", ex.Message.ToString());
                throw ex;
            }
        }
        public async Task<PreventionModel> CheckPreventedOrganization(int organizationId)
        {
            PreventionModel preventionModel = new();
            var preventionDetails = await _eServicesContext
                                    .Set<Appprev>()
                .Where(o => o.OrganizationId == organizationId && DateTime.Now.Date <= o.PreventionDate.Date)
                .FirstOrDefaultAsync();
            if (preventionDetails != null)
            {
                var bypassingTimes = await _eServicesContext.Set<InspectionAppointmentsPenaltyBypassing>()
                                    .Where(b => b.OrganizationId == organizationId &&
                                                b.BypassEndDate.Date >= DateTime.Now.Date &&
                                                b.BypassStartDate.Date <= DateTime.Now.Date)
                                    .FirstOrDefaultAsync();
                if (bypassingTimes is null)
                {
                    preventionModel.IsPrevented = true;
                    preventionModel.PreventionDate = preventionDetails.PreventionDate;
                    return preventionModel;
                }
            }
            return null;
        }
        public async Task<List<long>> GenerateRequesDetailsId(int numberOfReferences = 1)
        {
            try
            {
                DynamicParameters dapperParameters = new DynamicParameters();
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.DataSourceName.Name, "EServiceRequestsDetails");
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.SeedValue.Name, numberOfReferences);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name, dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name, dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);
                var result = await _dapper.Get<long>(StoredProcedureNames.MCPKCounters.Name, dapperParameters);

                var requestIdStart = dapperParameters.Get<long>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name);
                var requestIdEnd = dapperParameters.Get<long>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name);

                List<long> getRequestDeatilsIds = new List<long>();
                for (long i = requestIdStart; i <= requestIdEnd; i++)
                {
                    getRequestDeatilsIds.Add(i);
                }
                return getRequestDeatilsIds;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public async Task<List<VehiclesDTO>> GetLastInspectionPointsForVehicles(List<VehiclesDTO> vehicles)
        {
            var driverPassports = vehicles.Select(v => v.DriverPassportNumber).ToList();
            var driverCivilIds = vehicles.Select(v => v.DriverCivilId).ToList();
            var vehiclesPlatenumbers = vehicles.Select(v => new { v.PlateNo, v.Country }).ToList();//"Passport : "123456kjhgfdn" VehiclePlate: "124 7654gjkln"
            var includedStates = new List<int>() { (int)InspectionAppointmentStateEnum.Booked, (int)InspectionAppointmentStateEnum.Attended, (int)InspectionAppointmentStateEnum.Release };

            var lastInspectionAppointments = await _eServicesContext.Set<InspectionAppointments>()
                                                .Where(i => includedStates.Contains(i.StateId) &&
                                                            ((driverCivilIds.Contains(i.DriverCivilId) && i.DriverCivilId != null) ||
                                                            (driverPassports.Contains(i.DriverPassportNumber) && i.DriverPassportNumber != null) ||
                                                            vehiclesPlatenumbers.Select(vp => vp.PlateNo).ToList().Contains(i.VehiclePlateNumber))
                                                      )
                                                .OrderByDescending(i => i.InspectionDate)
                                                .ToListAsync();
            vehicles.ForEach(v =>
            {
                v.LastInspectionDate = lastInspectionAppointments.FirstOrDefault(i => (i.DriverCivilId == v.DriverCivilId && !string.IsNullOrEmpty(v.DriverCivilId))||
                                                                                      (i.DriverPassportNumber == v.DriverPassportNumber && !string.IsNullOrEmpty(v.DriverPassportNumber)) ||
                                                                                      (i.VehiclePlateNumber == v.PlateNo && i.Country == v.Country)
                                                                                )?.InspectionDate;
            });
            return vehicles;
        }

        public async Task<PortListsForInspectionAppointment> GetInspectionPortDetails(int portId)
        {
            return await _eServicesContext.Set<PortListsForInspectionAppointment>()
                                        .Where(p => p.PortId == portId)
                                 .FirstOrDefaultAsync();
        }
        public async Task<List<InspectionAppointmentMemoryModel>> GetAppointments(DateTime currentDateTime)
        {
            return await _eServicesContext.Set<InspectionAppointments>()
            .Where(a => a.InspectionDate.Date >= currentDateTime && a.StateId == (int)InspectionAppointmentStateEnum.Booked)
            .Select(a => new InspectionAppointmentMemoryModel
            {
                Date = a.InspectionDate,
                Time = a.InspectionTime,
                DeclarationPortId = a.DeclarationPortId,
                InpsectionPortId = a.InpsectionPortId,
                RampNumber = a.InspectionRampNumber,
                InspectionTokenCounter = a.InspectionTokenCounter,
                InspectionToken = a.InspectionToken
            }).ToListAsync();
        }
        public async Task<int> GetOrganizationId(int userId)
        {
            if (userId == 0)
            {
                return 0;
            }

            var orgId = await _eServicesContext.Set<MobileUserOrgMap>()
                .Where(a => _eServicesContext.Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                    .Any(b => b.UserId == a.UserId && b.LicenseNumber == a.ParentOrgTradeLicence)
                    && a.UserId == userId
                    && a.IsActive.HasValue
                    && a.IsActive.Value)
                .OrderByDescending(a => a.OrganizationId)
                .Select(a => a.OrganizationId)
                .FirstOrDefaultAsync();

            return orgId ?? 0;
        }
        private async Task<List<ReturnModel>> InititalReservceAppointments(
                DateTime date,
                TimeSpan? time,
                bool isHourly,
                List<string> vehicles,
                string declarationNumber,
                int inspectionPortStartRampNumber,
                int inspectionPortEndRampNumber,
                int inspectionPortCapacity,
                SequanceTypeEnum inspectionPortSequanceType,
                int inspectionPortId,
                int declarationPortId,
                string portCode,
                int portMaxCapacity)
        {
            var currentDateTime = DateTime.Now;
            List<InspectionAppointmentMemoryModel> appointments;
            appointments = await GetAppointments(currentDateTime);
            var portAppointments = appointments.Where(a => a.DeclarationPortId == declarationPortId &&
                                                            a.InpsectionPortId == inspectionPortId &&
                                                            a.Date.Date == date.Date &&
                                                            (isHourly ? a.Time == time : 1 == 1))
                                .ToList();

            _requestLogger.LogInformation($"ReservationData: Date:{date} ,Time:{time}, isHourly:{isHourly}, vehiclesCount:{vehicles.Count}, startRampNumber:{inspectionPortStartRampNumber}," +
                $"EndRampNumber:{inspectionPortEndRampNumber}, portMaxCapacity:{portMaxCapacity}");
            if (portAppointments.Count >= portMaxCapacity)
            {
                _requestLogger.LogException(new BusinessRuleException($"portAppointments.Count >= portMaxCapacity,Date:{date}, Time: {time}, Count:{portAppointments.Count} ,portMaxCapacity:{portMaxCapacity} "));
                throw new BusinessRuleException();
            }
            var res = new ReturnModel();
            var resultAppointments = new List<ReturnModel>();
            var counter = appointments.Count(a => a.DeclarationPortId == declarationPortId &&
                                                            a.InpsectionPortId == inspectionPortId &&
                                                            a.Date.Date == date.Date);
            List<RampAvailabilityModel> appointmentsAvailability = portAppointments
                .GroupBy(a => a.RampNumber)
                .Select(a => new RampAvailabilityModel
                {
                    RampNumber = a.Key,
                    ReservedAppointments = a.Count(),
                    InspectionPortCapacity = inspectionPortCapacity
                }).ToList();

            var incrment = inspectionPortSequanceType == SequanceTypeEnum.All ? 1 : 2;

            if (inspectionPortSequanceType == SequanceTypeEnum.Even && (inspectionPortStartRampNumber % 2) != 0)
            {
                ++inspectionPortStartRampNumber;
            }
            else if (inspectionPortSequanceType == SequanceTypeEnum.Odd && (inspectionPortStartRampNumber % 2) == 0)
            {
                ++inspectionPortStartRampNumber;
            }

            for (int ramp = inspectionPortStartRampNumber; ramp <= inspectionPortEndRampNumber; ramp = ramp + incrment)
            {
                if (!appointmentsAvailability.Any(a => a.RampNumber == ramp))
                {
                    appointmentsAvailability.Add(new RampAvailabilityModel
                    {
                        RampNumber = ramp,
                        ReservedAppointments = 0,
                        InspectionPortCapacity = inspectionPortCapacity
                    });
                }
            }

            if (appointmentsAvailability.Count(a => a.IsAvailable) < vehicles.Count)
            {
                _requestLogger.LogException(new BusinessRuleException($"no available capacity in the selected date, Date:{date}, Time: {time},inspectionPortStartRampNumber:{inspectionPortStartRampNumber}, "));
                throw new BusinessRuleException();
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                var availableRamp = appointmentsAvailability.OrderBy(a => a.RampNumber).First(a => a.IsAvailable).RampNumber;
                var token = "INS/" +
                            availableRamp.ToString("D2") +
                            "/" +
                            portCode +
                            "/" +
                            (++counter).ToString("D3");

                appointmentsAvailability.First(a => a.RampNumber == availableRamp).ReservedAppointments++;

                appointments.Add(new InspectionAppointmentMemoryModel
                {
                    Date = date,
                    Time = time,
                    DeclarationPortId = declarationPortId,
                    InpsectionPortId = inspectionPortId,
                    InspectionToken = token,
                    InspectionTokenCounter = counter,
                    RampNumber = availableRamp
                });
                resultAppointments.Add(new ReturnModel
                {
                    InspectionDate = date,
                    InspectionTime = time,
                    DeclarationPortId = declarationPortId,
                    InpsectionPortId = inspectionPortId,
                    InspectionToken = token,
                    InspectionTokenCounter = counter,
                    RampNumber = availableRamp,
                    VehicleId = vehicles[i],
                    DeclarationVehicleId = declarationNumber,
                    bookingDate = date,
                    InspectionTokenDate = date,
                    isCreate = true
                });
            }
            return resultAppointments;

        }

        public async Task<InspectionAppPreparationModel> PrepareInspectionAppointmentsModels(inspectionAppointmentsQueue appointmentQueue, List<VehiclesDTO> allDeclarationVehicles)
        {
            InspectionAppPreparationModel inspectionModel = new();
            inspectionModel.DeclarationVehicles = new();
            var declarationVehicleIds = appointmentQueue.DeclarationVehiclesIds.Split(",").
                                                        Select(i => Convert.ToInt32(i)).
                                                        ToList();
            string declarationNumber = allDeclarationVehicles.FirstOrDefault()?.DeclarationNumber;
            int declarationId = Convert.ToInt32(allDeclarationVehicles.FirstOrDefault()?.DeclarationId);
            int orgId = Convert.ToInt32(allDeclarationVehicles.FirstOrDefault()?.ConsigneeOrgId);
            int portId = Convert.ToInt32(allDeclarationVehicles.FirstOrDefault()?.PortId);
            int declarationTypeId = Convert.ToInt32(allDeclarationVehicles.FirstOrDefault()?.DeclarationTypeId);
            var inspectionPortDetails = await GetInspectionPortDetails(portId);
            var location = await _eServicesContext.Set<Location>().Where(a => a.LocationId == portId).FirstOrDefaultAsync();
            inspectionModel.UserId = appointmentQueue.UserID;
            inspectionModel.DeclarationNumber = declarationNumber;
            inspectionModel.DateCreated = appointmentQueue.DateCreated;
            inspectionModel.DeclarationId = declarationId;
            inspectionModel.OrgId = orgId;
            inspectionModel.DeclarationPortId = portId;
            inspectionModel.InspectionPortId = inspectionPortDetails.InspectionPortId;
            inspectionModel.IsHourlySet = inspectionPortDetails.IsHourlySet;
            inspectionModel.PortMaximumCapacity = inspectionPortDetails.MaxInspections;
            inspectionModel.DeclarationTypeId = declarationTypeId;
            inspectionModel.StartRampNumber = inspectionPortDetails.StartRampNumber;
            inspectionModel.EndRampNumber = inspectionPortDetails.EndRampNumber;
            inspectionModel.SequenceType = inspectionPortDetails.SequenceType;
            inspectionModel.LocationCode = location.LocationCode;
            inspectionModel.PortCapacityPerRamp = inspectionPortDetails.CapacityPerRamp;

            declarationVehicleIds.ForEach(Id =>
            {
                var vehicleData = allDeclarationVehicles.Where(v => v.VehicleId == Id.ToString()).FirstOrDefault();
                var vehicle = new VehiclesDTO()
                {
                    VehicleId = Id.ToString(),
                    PlateNo = vehicleData.PlateNo,
                    Weight = vehicleData.Weight,
                    DriverPassportNumber = vehicleData.DriverPassportNumber,
                    DriverCivilId = vehicleData.DriverCivilId,
                    Country = vehicleData.Country,
                    ConsigneeOrgId = vehicleData.ConsigneeOrgId,
                    ContainerNo = vehicleData.ContainerNo,
                    DeclarationId = vehicleData.DeclarationId,
                    DeclarationNumber = vehicleData.DeclarationNumber,
                    PortId = portId,
                    IsHourly = inspectionPortDetails.IsHourlySet,
                    LastInspectionDate = vehicleData.LastInspectionDate

                };
                inspectionModel.DeclarationVehicles.Add(vehicle);
            });

            var vehcilesPlateNumbers = inspectionModel.DeclarationVehicles.Select(v => v.PlateNo).ToList();
            var driversCivilIds = inspectionModel.DeclarationVehicles.Select(v => v.DriverCivilId).ToList();
            var driversPassports = inspectionModel.DeclarationVehicles.Select(v => v.DriverPassportNumber).ToList();
            var includedStates = new List<int>() { (int)InspectionAppointmentStateEnum.Booked, (int)InspectionAppointmentStateEnum.Attended, (int)InspectionAppointmentStateEnum.Release };
            var vehiclesDriversLastInspectionDate = await _eServicesContext
                                                        .Set<InspectionAppointments>()
                                                        .Where(I => includedStates.Contains(I.StateId) &&
                                                                    (vehcilesPlateNumbers.Contains(I.VehiclePlateNumber) ||
                                                                    (driversCivilIds.Contains(I.DriverCivilId) && !string.IsNullOrEmpty(I.DriverCivilId)) ||
                                                                    (driversPassports.Contains(I.DriverPassportNumber) && !string.IsNullOrEmpty(I.DriverPassportNumber)))
                                                                )
                                                        .OrderByDescending(I => I.InspectionDate)
                                                        .ToListAsync();

            inspectionModel.DeclarationVehicles.ForEach(v =>
            {
                var lastInspection = vehiclesDriversLastInspectionDate.Where(i =>
                                                    (v.PlateNo == i.VehiclePlateNumber && i.Country == v.Country) ||
                                                    (v.DriverCivilId == i.DriverCivilId && !string.IsNullOrEmpty(v.DriverCivilId)) || (v.DriverPassportNumber == i.DriverPassportNumber) && !string.IsNullOrEmpty(v.DriverPassportNumber))
                                                    .OrderByDescending(I => I.InspectionDate)
                                                    .FirstOrDefault();

                v.LastInspectionDate = lastInspection?.InspectionDate;
            });
            return inspectionModel;
        }
        public List<VehiclesDTO> GetVehiclesListForDeclaration(inspectionAppointmentsQueue appointmentQueue, VehiclesDTO vehiclesDTO)
        {
            ReqObj reqObj = new ReqObj()
            {
                CommonData = vehiclesDTO.DeclarationNumber,
                CommonData2 = appointmentQueue.UserID.ToString(),
                OrgID = vehiclesDTO.ConsigneeOrgId.ToString(),
            };
            var data = getVehicleListFromDO(reqObj);
            List<VehiclesDTO> vehiclesList = new List<VehiclesDTO>();
            if (data.Tables != null && data.Tables?.Count > 0)
            {
                if (data.Tables["vehiclelist"].Rows.Count > 0 && data.Tables["vehiclelist"].Columns.Count > 2)
                {
                    vehiclesList = data.Tables[0].AsEnumerable().Select(v => new VehiclesDTO
                    {
                        VehicleId = v["VehicleID"].ToString(),
                        ContainerNo = v["ContainerNumber"].ToString(),
                        DriverMobileNo = v["MobileNumber"].ToString(),
                        DriverName = v["DriverName"].ToString(),
                        DriverCivilId = string.IsNullOrWhiteSpace(v["DriverCivilId"].ToString()) ? null : v["DriverCivilId"].ToString(),
                        DriverPassportNumber = string.IsNullOrWhiteSpace(v["DriverPassportNumber"].ToString()) ? null : v["DriverPassportNumber"].ToString(),
                        PlateNo = v["VehiclePlateNumber"].ToString(),
                        Weight = v["weight"].ToString(),
                        Country = Convert.ToInt32(v["Country"]),
                        PortId = Convert.ToInt32(v["portid"].ToString()),
                        DeclarationTypeId = (v["DeclarationType"].ToString()).Contains("Import") ? (int)BillTypesEnums.Import : (int)BillTypesEnums.Export, //Convert.ToInt32(request._reqObj.DeclarationTypeId),
                        DeclarationId = v["DeclarationId"].ToString(),
                        DeclarationNumber = vehiclesDTO.DeclarationNumber,
                        ConsigneeOrgId = vehiclesDTO.ConsigneeOrgId
                    }).ToList();

                }

            }
            return vehiclesList;
        }
        public async Task<List<GetRequestNumberModel>> GenerateIdForInspectionAppointment(string type, string port, int numberOfReferences = 1)
        {
            try
            {
                DynamicParameters dapperParameters = new DynamicParameters();


                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.DataSourceName.Name, "EServiceRequests_pk");


                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.SeedValue.Name, numberOfReferences);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name, dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name, dbType: System.Data.DbType.Int64, direction: System.Data.ParameterDirection.Output);
                var result = await _dapper.Get<int>(StoredProcedureNames.MCPKCounters.Name, dapperParameters);

                var requestIdStart = dapperParameters.Get<Int64>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name);
                var requestIdEnd = dapperParameters.Get<Int64>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.DataSourceName.Name, ServiceHelpers.GtServiceCounterName(ServiceTypesEnum.InspectionAppointments));
                if (port == DeclarationPortEnum.Shuwaikh.GetEnumDescription())
                {
                    dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.DataSourceName.Name, "InspectionAppointmentSwk");

                }
                else
                {
                    dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.DataSourceName.Name, "InspectionAppointmentSal");

                }
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.SeedValue.Name, numberOfReferences);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name, dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
                dapperParameters.Add(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name, dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
                result = await _dapper.Get<int>(StoredProcedureNames.MCPKCounters.Name, dapperParameters);


                var requestNumberStart = dapperParameters.Get<int>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueStart.Name);

                var requestNumberEnd = dapperParameters.Get<int>(StoredProcedureNames.MCPKCounters.MCPKCountersParamerters.CounterValueEnd.Name);

                List<GetRequestNumberModel> getRequestNumbers = new List<GetRequestNumberModel>();
                for (long i = requestIdStart; i <= requestIdEnd; i++)
                {
                    getRequestNumbers.Add(new GetRequestNumberModel
                    {
                        RequestId = i,
                        RequestNumber = ServiceHelpers.GtServiceCode(ServiceTypesEnum.InspectionAppointments, requestNumberStart, DateTime.Now.ToString(DateTimeFormats.DefaultYearFormatyy), port),

                    });
                    requestNumberStart++;
                }

                return getRequestNumbers;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public DataSet getVehicleListFromDO(ReqObj R)
        {
            DataSet dataSet = new DataSet();
            try
            {
                using SqlConnection connection = new SqlConnection(_configurations.ConnectionStrings.Default);
                using SqlCommand sqlCommand = new SqlCommand("etrade.getVehicleListFromDO", connection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.Add("@DeclNumber", SqlDbType.VarChar).Value = R.CommonData;
                sqlCommand.Parameters.Add("@TempDeclNumber", SqlDbType.VarChar).Value = R.CommonData1;
                sqlCommand.Parameters.Add("@OrgId", SqlDbType.BigInt).Value = R.OrgID;
                sqlCommand.Parameters.Add("@MobileUserId", SqlDbType.BigInt).Value = R.CommonData2;
                new SqlDataAdapter(sqlCommand).Fill(dataSet);
                for (int i = 0; i < dataSet.Tables.Count; i++)
                {
                    if (dataSet.Tables[i].Columns.Contains("TableName"))
                    {
                        if (dataSet.Tables[i].Rows.Count > 0)
                        {
                            dataSet.Tables[i].TableName = dataSet.Tables[i].Rows[0]["TableName"].ToString();
                        }
                    }
                    else
                    {
                        dataSet.Tables.RemoveAt(i);
                        i--;
                    }
                }

                return dataSet;
            }
            catch (Exception ex)
            {
                CommonFunctions.LogUserActivity("getVehicleListFromDO", "", "", "", "", ex.Message.ToString());
                throw ex;
            }
        }
        #region Models
        public class InspectionAppointmentMemoryModel
        {
            public DateTime Date { get; set; }
            public TimeSpan? Time { get; set; }
            public int DeclarationPortId { get; set; }
            public int InpsectionPortId { get; set; }
            public int RampNumber { get; set; }
            public string InspectionToken { get; set; }
            public int InspectionTokenCounter { get; set; }
        }

        public class InspectionAppPreparationModel
        {
            public List<VehiclesDTO> DeclarationVehicles { get; set; } //done
            public int UserId { get; set; }//done
            public int DeclarationId { get; set; }//done
            public DateTime DateCreated { get; set; } //done
            public DateTime? InspectionDate { get; set; } //done
            public TimeSpan? InspectionTime { get; set; } //done
            public string DeclarationNumber { get; set; }//done
            public int DeclarationTypeId { get; set; }//done
            public int StartRampNumber { get; set; }
            public int EndRampNumber { get; set; }
            public SequanceTypeEnum SequenceType { get; set; }
            public int DeclarationPortId { get; set; }//done
            public int InspectionPortId { get; set; }
            public int PortCapacityPerRamp { get; set; }
            public string LocationCode { get; set; }
            public int PortMaximumCapacity { get; set; }
            public int OrgId { get; set; }//done
            public bool IsHourlySet { get; set; }//done
        }
        public class VehiclesDTO
        {
            public string VehicleId { get; set; }
            public string PlateNo { get; set; }
            public string ContainerNo { get; set; }
            public string Weight { get; set; }
            public string DriverName { get; set; }
            public string DriverMobileNo { get; set; }
            public int PortId { get; set; }
            public int DeclarationTypeId { get; set; }
            public string DeclarationId { get; set; }

            public string DriverCivilId { get; set; }
            public string DriverPassportNumber { get; set; }
            public DateTime? LastInspectionDate { get; set; }
            public int Country { get; set; }

            public string DeclarationType { get; set; }

            public string VehicleAppointmentState { get; set; }

            public string DeclarationNumber { get; set; }

            public string PostCode { get; set; }

            public int StateId { get; set; }

            public string State { get; set; }

            public bool IsHourly { get; set; }

            public int ConsigneeOrgId { get; set; }
            public DateTime? InspectionDate { get; set; } //done
            public TimeSpan? InspectionTime { get; set; } //done

        }
        #endregion
    }
}
