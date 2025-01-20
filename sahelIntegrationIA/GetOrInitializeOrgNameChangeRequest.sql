USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[GetOrInitializeOrgNameChangeRequest]    Script Date: 1/19/2025 10:30:14 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER      PROC [etrade].[GetOrInitializeOrgNameChangeRequest]  
(@UserId varchar(100),@LanguageId varchar(5))  
as  
BEGIN  
  
--SELECT * FROM etrade.Eservices    
  Declare @EserviceRequestId int   
  DECLARE @serviceid int =41 -- (SELECT ServiceID FROM etrade.Eservices   WHERE ServiceNameEng=N'Org Name Change')   
  DECLARE @OrgId nvarchar(200)  
SET  @OrgId=(SELECT top 1 a.ORGANIZATIONID FROM etrade.mobileuserorgmaps a INNER JOIN etrade.mobileuser b   
on  a.userid=b.userid AND a.ParentOrgTradeLicence=b.LicenseNumber WHERE a.isActive=1 and 
a.userid=@UserId ORDER BY a.mobileuserorgmapid desc)--(SELECT ORGANIZATIONID FROM etrade.mobileuserorgmaps WHERE userid=@UserId )--AND etrade.mobileuserorgmaps.isActive=1)  
  
--DECLARE @LatestImportLicense nvarchar(50)=(SELECT top 1  
--   ImporterLicenseNo  FROM [OrgImporterLicense] WHERE OrganizationId=@OrgId   
--  AND stateid='OrgImporterLicenseActivatedState'  
--  ORDER BY dbo.OrgImporterLicense.DateModified desc)  
  
IF EXISTS(SELECT 1 FROM ETRADE.MOBILEUSERORGMAPS WHERE USERID=@UserId)  
BEGIN  
--SELECT 1 'OrgAvailable'  
SELECT  DISTINCT C.COMPANYCIVILID,A.TradeLicenseNumber,A.ImporterLicenseNo,--@LatestImportLicense 'ImporterLicenseNo',--  
A.IndustrialLicenseNo,  
A.CommercialLicenseNo,1 'OrgAvailable','OrgBasic' TableName FROM ORGANIZATIONS A INNER JOIN ETRADE.MOBILEUSERORGMAPS B ON A.OrganizationId=B.OrganizationId  
INNER JOIN ETRADE.MOBILEUSER C ON C.USERID=B.USERID WHERE c.userid=@UserId  
END  
ELSE  
BEGIN  
SELECT 0 'OrgAvailable','OrgBasic' TableName  
return;  
END  
  
--not used for now  
IF  EXISTS (                  
     SELECT 1                  
     FROM [etrade].[EServiceRequests]                  
     WHERE  
  etrade.EServiceRequests.RequesterUserId=@UserId AND etrade.EServiceRequests.ServiceId=@serviceid  
  AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGSubmittedState','EServiceRequestORGReSubmittedState')--,'EServiceRequestCreatedState')   -- create when there is no active request or no ongoing request              
     )  
  or   
 --exists( etrade.OrganizationUpdateRequestCurrentlyActiveFunc(@OrgId))  
   exists(select 1 from etrade.organizationrequests   
  where OrganizationId = @OrgId   AND requestnumber LIKE '%OR/%' AND--Checking only organization request stateid    
 (StateId in ('OrganizationRequestSubmittedState','OrganizationRequestRejectedState','OrganizationRequestedForAdditionalInfoState','OrganizationRequestForCreateState','OrganizationRequestForUpdateState')--,'OrganizationRequestRejectedState')  
  OR ( stateid IN ('OrganizationRequestApprovedForUpdate') AND  isnull(IsUpdated,0)<>'1')-- checking if the update has been done by MCSupport after update approval  
  )  
 )  
  
  BEGIN  
  DECLARE @OrganizationUpdateRequestCurrentlyActive varchar='0'  
  IF  exists(select 1 from etrade.organizationrequests   
  where OrganizationId = @OrgId    AND requestnumber LIKE '%OR/%' AND--Checking only organization request stateid of any update uncompleted request present  
  StateId in ('OrganizationRequestApprovedForUpdate','OrganizationRequestSubmittedState','OrganizationRequestRejectedState','OrganizationRequestedForAdditionalInfoState',
  'OrganizationRequestForCreateState','OrganizationRequestForUpdateState')--,'OrganizationRequestRejectedState')  
  )  
  BEGIN  
  SET @OrganizationUpdateRequestCurrentlyActive='1'  
  END  
  
  
  
  SELECT 0 'CanCreateRequest',@OrganizationUpdateRequestCurrentlyActive 'OrganizationUpdateRequestCurrentlyActive','RequestAccess' TableName-- show message to go to see submitted active request   
  return;  
  END  
  ELSE  
  BEGIN  
  SELECT 1 'CanCreateRequest','RequestAccess' TableName-- allow to create or edit pending request  
  END  
  
  
  
 DECLARE @EServiceRequestIdParam VARCHAR(100)  
  
IF not EXISTS (                  
     SELECT 1                  
     FROM [etrade].[EServiceRequests]                  
     WHERE-- etrade.EServiceRequests.EServiceRequestNumber = @RequestNumber AND   
  etrade.EServiceRequests.RequesterUserId=@UserId AND etrade.EServiceRequests.ServiceId=@serviceid  
  --AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGCreatedState','EServiceRequestORGRejectedState') --'EServiceRequestSubmittedState','EServiceRequestReSubmittedState',  -- create when there is no active request or no ongoing request          
    
    AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGCreatedState','EServiceRequestORGForAdditionalInfo','EServiceRequestORGRejectedState')
	 --'EServiceRequestSubmittedState','EServiceRequestReSubmittedState',  -- create when there is no active request or no ongoing request              
     )   
   BEGIN   
  
--SELECT a.organizationid,a.organizationtypeid,a.name 'OldOrgEngName',a.organizationcode,a.description,a.tradelicensenumber,  
--b.Name 'OldOrgAraName'  
-- FROM organizations a INNER JOIN organizations_ara b ON a.OrganizationId=b.OrganizationId   
--  WHERE  a.ORGANIZATIONID=@OrgId  
  
  ---Create Request   
  
 declare @RequestdataSourceName varchar(100)                 
 DECLARE @prefix VARCHAR(20)   
     
  
 SET @prefix = (                  
    SELECT prefix                  
    FROM etrade.Eservices                  
    WHERE ServiceID = @serviceid                  
                  
    )           
         
  SET @RequestdataSourceName = (                  
    SELECT RequestDataSourceName                  
    FROM etrade.Eservices                  
    WHERE ServiceID = @serviceid                  
                  
    )     
  
  
 DECLARE @CounterValueStartPkidIDR INT = 0                  
   ,@CounterValuePkEndR INT = 0                  
                  
  EXEC dbo.usp_MCPKCounters @DataSourceName = 'EServiceRequests_pk'                  
   ,@CounterValueStart = @CounterValueStartPkidIDR OUTPUT                  
   ,@CounterValueEnd = @CounterValuePkEndR OUTPUT -- bigint                    
                  
    --SELECT * FROM dbo.SGCounter  WHERE tablename LIKE '%eservice%'  
            --select @RequestdataSourceName      
  
    DECLARE @CounterValueStart INT = 0                  
   ,@CounterValueEnd INT = 0     
  
  EXEC dbo.usp_MCPKCounters @DataSourceName = @RequestdataSourceName                  
   ,@CounterValueStart = @CounterValueStart OUTPUT                  
   ,@CounterValueEnd = @CounterValueEnd OUTPUT -- bigint           
   --select  @CounterValueStart ,@CounterValueEnd             
      INSERT INTO [etrade].[EServiceRequests] (                  
    EServiceRequestid                  
    ,eservicerequestnumber                  
    ,serviceid                  
    ,DateCreated                  
    ,DateModified                  
    ,stateid                  
    ,createdby                  
   ,RequesterUserId                  
    )                 
   SELECT TOP 1 @CounterValueStartPkidIDR                  
    ,(@prefix + '/' + convert(VARCHAR(100), @CounterValueStart) + '/' + convert(VARCHAR(max), right(year(GETDATE()), 2)))                  
    ,@serviceid                  
    ,GETDATE() AS [DateCreated]                  
    ,null --qasem-23-4                  
    ,'EServiceRequestORGCreatedState'                  
    ,@Userid                  
    ,@Userid                  
  PRINT @@ROWCOUNT                  
                  
   IF EXISTS (                  
     SELECT 1                  
     FROM [etrade].[EServiceRequests]                  
     WHERE EServiceRequestid = @CounterValueStartPkidIDR                  
     ) --@@ROWCOUNT = '1')                  
    --PRINT ('insidedetails');                  
   BEGIN                  
    DECLARE @ForeignEserviceid INT                  
                  
    SET @ForeignEserviceid = @CounterValueStartPkidIDR;                  
    SET @CounterValueStart = 0                  
    SET @CounterValueEnd = 0                  
                  
    EXEC dbo.usp_MCPKCounters @DataSourceName ='EServiceRequestsDetails'-- @RequestDetailsdataSourceName                  
     ,@CounterValueStart = @CounterValueStart OUTPUT                  
  ,@CounterValueEnd = @CounterValueEnd OUTPUT                  
                  
    INSERT INTO [etrade].[EServiceRequestsdetails] (                  
     EServiceRequestdetailsid                  
     ,eservicerequestid                  
     ,organizationid                  
     ,stateid                  
     ,createdby                  
     ,modifiedby                  
     ,RequestForUserId                  
     ,RequestForName                  
    ,RequestForEmail -- No mcuserid and email id for messengers, so RequesterMobileruserid email will be considered                  
     ,RequestServicesId                  
     ,RequesterUserid     
  ,OldOrgEngName  
  ,OldOrgAraName  
  ,LicenseNumber  
              
     --,RequestForUserType,CivilID,LicenseNumber,UTFBrokerPersonalId  
  )                  
    VALUES (                  
     @CounterValueStart                  
     ,@ForeignEserviceid                  
     ,@orgid                  
     ,'EServiceRequestDetailsORGCreatedState'--'EServiceDetailsRequestCreatedState'                  
     ,@Userid                  
     ,''                  
     ,NULL--@RequestedForMobileUserid--considering case it can be for other non app users too                  
    --,(SELECT  FirstName +' '+LastName FROM ETRADE.MobileUser WHERE UserId=@RequestedForMobileUserid)                  
    ,isnull( (SELECT  Name FROM Organizations WHERE OrganizationId  =@OrgId),                  
    (SELECT  FirstName +' '+LastName FROM ETRADE.MobileUser WHERE UserId=@Userid))                  
    ,
	--isnull( (SELECT a.EMAILID  FROM users a inner join   Organizations b on a.OrganizationId=b.OrganizationId  
 -- WHERE a.OrganizationId  =@OrgId),                  
    (SELECT  EmailId FROM ETRADE.MobileUser WHERE UserId=@Userid)
	--)                  
     ,@serviceid                  
     ,@Userid  
  ,(  
  SELECT a.name  FROM organizations a    
  WHERE  a.ORGANIZATIONID=@OrgId)  
  , (  
  SELECT a.Name  FROM organizations_ara a   
  WHERE  a.ORGANIZATIONID=@OrgId)  
  ,(SELECT a.TradeLicenseNumber  FROM organizations a INNER JOIN organizations_ara b ON a.OrganizationId=b.OrganizationId   
  WHERE  a.ORGANIZATIONID=@OrgId)  
  --,@BrokerTypeId,@civilIdvalue,@licenseNovalue,@BrokerPersonalId  
  )                  
     --SET @eservicerequestid=@ForeignEserviceid                
          
                                             
/* audit entries for eserviceRequest and eserviceRequestDetails - start */                                       
           
            
 INSERT INTO ETRADE.[$EServiceRequests] ([$AuditTrailId], [$UserId], [$Operation], [$DateTime], [$DataProfileClassId], [$IPId],          
   [$SessionId], [EServiceRequestId], [EServiceRequestNumber], [RequestSubmissionDateTime], [RequestCompletionDateTime], [StateId],          
    [DateCreated], [CreatedBy], [ModifiedBy], [DateModified], [OwnerOrgId], [OwnerLocId], [ServiceId], [RequesterUserId],           
    [RequesterUserType], [DeliveredDate], [DeliveredBy], [ApprovedDate], [ApprovedBy], [KNetReceiptNo])          
 SELECT NEWID(), isnull(@Userid,@Userid), 1, GETDATE(), 'EServiceRequests', '127.0.0.1', 'system', [EServiceRequestId],           
  [EServiceRequestNumber], [RequestSubmissionDateTime], [RequestCompletionDateTime], [StateId], GETDATE(), @Userid,           
  isnull(@Userid,@Userid), GETDATE(), [OwnerOrgId], [OwnerLocId], [ServiceId], [RequesterUserId], [RequesterUserType], [DeliveredDate],          
   [DeliveredBy], [ApprovedDate], [ApprovedBy], [KNetReceiptNo] FROM ETRADE.EServiceRequests   
   WHERE EServiceRequestId =@ForeignEserviceid-- @EServiceRequestId          
          
 INSERT INTO ETRADE.[$EServiceRequestsDetails] ([$AuditTrailId], [$UserId], [$Operation], [$DateTime], [$DataProfileClassId], [$IPId], [$SessionId], [EserviceRequestDetailsId],
  [EserviceRequestId], [RequestForUserType], [RequestServicesId], [OrganizationId], [RequesterLicenseNumber], [RequesterArabicName], [RequesterEnglishName],
 [ArabicFirstName], [ArabicSecondName], [ArabicThirdName], [ArabicLastName], [EnglishFirstName], 
[EnglishSecondName], [EnglishThirdName], [EnglishLastName], [Nationality], [CivilID], [CivilIDExpiryDate], [MobileNumber], [PassportNo], [PassportExpiryDate],
 [Address], [Email], [LicenseNumber], [LicenseNumberExpiryDate], [Remarks], [RejectionRemarks], 
[RequestForVisit], [RequestForVisitRemarks], [ExamAddmissionId], [ExamDetailsId], [status], [StateId], [DateCreated], [CreatedBy], [ModifiedBy], [DateModified],
 [OwnerOrgId], [OwnerLocId], [RequesterUserId], [RequestForName], [RequestForEmail], [Gender], 
[RequestForUserId], [BrokFileNo], [AssociatedOrgIds])          
 Select NEWID(), isnull(@Userid,@Userid), 1, GETDATE(), 'EServiceRequestsDetails', '127.0.0.1', 'system', [EserviceRequestDetailsId], [EserviceRequestId], [RequestForUserType],
  [RequestServicesId], [OrganizationId], [RequesterLicenseNumber], [RequesterArabicName], [RequesterEnglishName], [ArabicFirstName], [ArabicSecondName],
 [ArabicThirdName], [ArabicLastName], [EnglishFirstName], [EnglishSecondName], [EnglishThirdName],
 [EnglishLastName], [Nationality], [CivilID], [CivilIDExpiryDate], [MobileNumber], [PassportNo], [PassportExpiryDate], [Address], [Email], [LicenseNumber], 
[LicenseNumberExpiryDate], [Remarks], [RejectionRemarks], [RequestForVisit], [RequestForVisitRemarks],
 [ExamAddmissionId], [ExamDetailsId], [status], [StateId], [DateCreated], [CreatedBy], [ModifiedBy], [DateModified], [OwnerOrgId], [OwnerLocId],
 [RequesterUserId], [RequestForName], [RequestForEmail], [Gender], [RequestForUserId], [BrokFileNo],
 [AssociatedOrgIds] from etrade.EServiceRequestsDetails WHERE EServiceRequestId = @ForeignEserviceid--@EServiceRequestId           
          
          
/* audit entries for eserviceRequest and eserviceRequestDetails - start */          
     
  ---Create REquest  
  
  SELECT a.RequesterUserId 'UserId',a.EServiceRequestId,a.EServiceRequestNumber, b.oldorgengname,b.oldorgaraname,b.neworgengname,b.neworgaraname,  
  b.LicenseNumber,b.OrganizationId,a.stateid,'1' UpdateRequest,'1' EditableRequest,
  b.AuthorizerIssuer,b.Nationality,b.AuthorizedSignatoryCivilIdExpiryDate,b.CivilID,b.IssueDate,b.ExpiryDate,b.AuthorizedPerson	,-- rama for authorizer issuer change 16-01-2025
  'RequestDetails' TableName FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId  
  WHERE a.RequesterUserId=@UserId AND a.ServiceId=@serviceid AND a.EServiceRequestId=@ForeignEserviceid  
    
    
  SET @EServiceRequestIdParam=@ForeignEserviceid  
  
    
    
  Declare @ADDOrganizationRequestId int =null,  
 @ADDRequestNumber nvarchar(50) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@ForeignEserviceid ),  
 @ADDRequesterOwner int =@userid,  
 @ADDName nvarchar(240) =NULL,  
 @ADDOrganizationId int =@orgid,  
 @ADDLocalDescription nvarchar(340) =NULL,  
 @ADDCivilId varchar(20) =NULL,  
 @ADDAuthPerson nvarchar(240) =NULL,  
 @ADDCommercialLicenseNo nvarchar(200) =null,  
 @ADDCommercialLicenseType int =null,  
 @ADDCommercialLicenseIssueDate datetime =NULL,  
 @ADDCommercialLicenseExpiryDate datetime =NULL,  
 @ADDImporterLicenseNo nvarchar(200) =null,  
 @ADDImporterLicenseType int =null,  
 @ADDImporterLicenseIssueDate datetime =NULL,  
 @ADDImporterLicenseExpiryDate datetime =NULL,  
 @ADDIndustrialLicenseNo varchar(50) =null,  
 @ADDIndIssueDate datetime =NULL,  
 @ADDIndExpiryDate datetime =NULL,  
 @ADDDateCreated datetime =getdate(),  
 @ADDCreatedBy varchar(35) =@userid,  
 @ADDStateId varchar(50) ='EServiceRequestORGCreatedState',  
 @ADDRequestedDate datetime =NULL,  
 @ADDEserviceRequestNumber varchar(100) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@ForeignEserviceid ),  
 @AuthPersonNationality nvarchar(100) =NULL,  
 @AuthPersonIssueDate nvarchar(100) =NULL,  
 @AuthPersonExpiryDate nvarchar(100) =NULL,  
  
 @ADDPostBoxNumber nvarchar(30) =NULL,  
 @ADDCity nvarchar(60) =NULL,  
 @ADDState nvarchar(60) =NULL,  
 @ADDPostalCode nvarchar(30) =NULL,  
 @ADDCountry int =NULL,  
 @ADDAddress nvarchar(1000) =NULL,  
 @ADDBlock nvarchar(200) =NULL,  
 @ADDStreet nvarchar(200) =NULL  
 ,@Floor                  NVARCHAR(200) = NULL,   
 @ADDApartmentType nvarchar(200) =NULL,  
 @ADDApartmentNumber nvarchar(200) =NULL,  
  
  
  
 @ADDBusinessTelNumber nvarchar(40) =NULL,  
 @ADDHomeTelNumber varchar(20) =NULL,  
 @ADDBusinessFaxNumber nvarchar(40) =NULL,  
 @ADDMobileTelNumber varchar(15) =NULL,  
 @ADDEMail varchar(50) =NULL,  
 @ADDWebPageAddress varchar(50) =NULL,  
 @ADDType nvarchar(50)='Start' --Start/Submit  
  
 EXEC etrade.ManageOrgServices @ADDOrganizationRequestId  ,  
 @ADDRequestNumber  ,  
 @ADDRequesterOwner  ,  
 @ADDName  ,  
 @ADDOrganizationId  ,  
 @ADDLocalDescription  ,  
 @ADDCivilId  ,  
 @ADDAuthPerson  ,  
 @ADDCommercialLicenseNo  ,  
 @ADDCommercialLicenseType,  
 @ADDCommercialLicenseIssueDate  ,  
 @ADDCommercialLicenseExpiryDate  ,  
 @ADDImporterLicenseNo  ,  
 @ADDImporterLicenseType  ,  
 @ADDImporterLicenseIssueDate  ,  
 @ADDImporterLicenseExpiryDate  ,  
 @ADDIndustrialLicenseNo  ,  
 @ADDIndIssueDate  ,  
 @ADDIndExpiryDate  ,  
 @ADDDateCreated  ,  
 @ADDCreatedBy  ,  
 @ADDStateId  ,  
 @ADDRequestedDate  ,  
 @ADDEserviceRequestNumber  ,  
 @AuthPersonNationality ,  
 @AuthPersonIssueDate  ,  
 @AuthPersonExpiryDate  ,  
  
 @ADDPostBoxNumber  ,  
 @ADDCity  ,  
 @ADDState  ,  
 @ADDPostalCode  ,  
 @ADDCountry  ,  
 @ADDAddress  ,  
 @ADDBlock  ,  
 @ADDStreet  ,  
 @Floor ,   
 @ADDApartmentType ,  
 @ADDApartmentNumber ,  
  
  
  
 @ADDBusinessTelNumber  ,  
 @ADDHomeTelNumber  ,  
 @ADDBusinessFaxNumber  ,  
 @ADDMobileTelNumber  ,  
 @ADDEMail  ,  
 @ADDWebPageAddress  ,  
 @ADDType  --Start/Submit  
  
  
  END  
    
  end  
  ELSE  
  BEGIN  
   
  SELECT a.RequesterUserId 'UserId',a.EServiceRequestId,a.EServiceRequestNumber,b.oldorgengname,b.oldorgaraname,b.neworgengname,b.neworgaraname,  
  b.LicenseNumber,b.OrganizationId,a.stateid,'0' UpdateRequest,'0' EditableRequest,'RequestDetails' TableName  FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId  
  WHERE a.RequesterUserId=@UserId AND a.ServiceId=@serviceid AND a.StateId IN ('EServiceRequestORGForAdditionalInfo','EServiceRequestORGCreatedState','EServiceRequestORGRejectedState')--'EServiceRequestORGRejectedState',  
    
  SET @EServiceRequestIdParam=(select a.EServiceRequestId  FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId  
  WHERE a.RequesterUserId=@UserId AND a.ServiceId=@serviceid  AND a.StateId IN ('EServiceRequestORGForAdditionalInfo','EServiceRequestORGCreatedState','EServiceRequestORGRejectedState'))--'EServiceRequestORGRejectedState',  
  
  END  
  
 SELECT top 1  
   Id,OrganizationId,ImporterLicenseNo,ImporterLicenseType,  
  FORMAT (LicenseIssueDate, 'dd-MM-yyyy') 'LicenseIssueDate',FORMAT (LicenseExpiryDate, 'dd-MM-yyyy') 'LicenseExpiryDate'  
  ,LicValidated,Sync,SyncDate,  
   
  'LicenseDetails' TableName FROM [OrgImporterLicense] WHERE OrganizationId=@OrgId   
  AND stateid='OrgImporterLicenseActivatedState'  
  ORDER BY LicenseIssueDate DESC  
  
  --exec etrade.GetOrgNameChangeReqDocuments @LanguageId,'M',@EServiceRequestIdParam  
    
exec [etrade].[getreqdocsddldataforeservices] @EServiceRequestIdParam,@LanguageId,'EserviceOrganizationNameChangeDocs'    
EXEC [etrade].[usp_GetUploadedDocumentsInfoForeservices] @LanguageId,'DocumentId','ScanRequestUploadDocs','M',@EServiceRequestIdParam,'','EserviceOrganizationNameChangeDocs',''  
--exec etrade.GetOrgNameChangeReqDocuments @LanguageId,'M',@EServiceRequestIdParam  
--exec [etrade].[getreqdocsddldataforeservices] '23','eng','BRsOrganizationNameChangeDocs'    
  
END  
