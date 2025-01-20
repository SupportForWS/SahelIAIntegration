USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[ManageOrgAuthorizedSignatories]    Script Date: 1/15/2025 6:49:02 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER    PROC [etrade].[ManageOrgAuthorizedSignatories]  
 (
 --@AuthSignatoryId bigint=NULL,   
  --@OrganizationId bigint=NULL,  
  --@AuthorizedPerson nvarchar(200)=NULL,  
  --@CivilID nvarchar(200)=NULL,  
  ----@TypeOfRequest nvarchar(200)=null,--Add/Renew/Remove
  @serviceid INT =0,--Add 46/Renew 47/Remove 48
  @UserId bigint=null,
  --@ScanRequestUploadDocId bigint=NULL,
  --@isActive bit =0,
  --@Sync bit =0,
  @LanguageId varchar(5)='ara'
  )  
 as  
 begin  
   
    --DECLARE @serviceid int =(SELECT ServiceID FROM etrade.Eservices WHERE ServiceNameEng=N'Add New Importer License') 
DECLARE @OrgId nvarchar(200)
SET  @OrgId=(SELECT top 1 a.ORGANIZATIONID FROM etrade.mobileuserorgmaps a INNER JOIN etrade.mobileuser b 
on  a.userid=b.userid AND a.ParentOrgTradeLicence=b.LicenseNumber WHERE a.userid=@UserId ORDER BY a.Mobileuserorgmapid desc)--(SELECT ORGANIZATIONID FROM etrade.mobileuserorgmaps WHERE userid=@UserId )--AND etrade.mobileuserorgmaps.isActive=1)

--CHECK MULTI ORG IN MOBILEORGTABLE CASE AND FIX

IF EXISTS(SELECT 1 FROM ETRADE.MOBILEUSERORGMAPS WHERE USERID=@UserId)
BEGIN

SELECT  DISTINCT C.COMPANYCIVILID,A.TradeLicenseNumber,A.ImporterLicenseNo,A.IndustrialLicenseNo,
A.CommercialLicenseNo,1 'OrgAvailable','OrgBasic' TableName FROM ORGANIZATIONS A INNER JOIN ETRADE.MOBILEUSERORGMAPS B ON A.OrganizationId=B.OrganizationId
INNER JOIN ETRADE.MOBILEUSER C ON C.USERID=B.USERID WHERE c.userid=@UserId
END
ELSE
BEGIN
SELECT 0 'OrgAvailable','OrgBasic' TableName
return;
END

--DECLARE @TotalAuthorizedSignatories int =(SELECT count(*) FROM etrade.OrgAuthorizedSignatories WHERE 
--etrade.OrgAuthorizedSignatories.StateId='' AND etrade.OrgAuthorizedSignatories.OrganizationId=@OrgId
--)

--DECLARE @TotalActiveAuthorizedSignatoriesRequest int =(SELECT count(a.CivilID) FROM etrade.EServiceRequestsDetails a inner JOIN
--etrade.EServiceRequests b ON a.EserviceRequestId=b.EServiceRequestId WHERE 
--b.ServiceId=@serviceid and
--b.StateId IN ('EServiceRequestORGSubmittedState','EServiceRequestORGReSubmittedState','EServiceRequestORGCreatedState','EServiceRequestORGRejectedState') 
--AND a.OrganizationId=@OrgId AND a.CivilID IS NOT null
--)



----IF  EXISTS (                
----     SELECT 1                
----     FROM [etrade].[EServiceRequests]  a INNER JOIN etrade.EServiceRequestsDetails b        ON a.EServiceRequestId=b.EserviceRequestId     
----     WHERE
----	 a.RequesterUserId=@UserId AND a.ServiceId=@serviceid
----	 --AND b.civilid =@CivilID
----	 AND a.StateId IN ('EServiceRequestSubmittedState','EServiceRequestReSubmittedState')--,'EServiceRequestCreatedState')   -- create when there is no active request or no ongoing request            
----     ) 
----	  
--If(	 @TotalActiveAuthorizedSignatoriesRequest=@TotalAuthorizedSignatories)
IF   exists(
  -- select 1 from etrade.organizationrequests 
	 --where OrganizationId = @OrgId AND 
	 --StateId in ('OrganizationRequestedForAdditionalInfoState','OrganizationRequestForCreateState','OrganizationRequestForUpdateState','OrganizationRequestRejectedState')

	  SELECT 1                
     FROM [etrade].[EServiceRequests]                
     WHERE
	 etrade.EServiceRequests.RequesterUserId=@UserId AND etrade.EServiceRequests.ServiceId=@serviceid
	 AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGSubmittedState','EServiceRequestORGReSubmittedState')--,'EServiceRequestCreatedState')   -- create when there is no active request or no ongoing request            
     )
	 or  exists(select 1 from etrade.organizationrequests 
	 where OrganizationId = @OrgId AND requestnumber LIKE '%OR/%' AND--Checking only organization request stateid
	 (StateId in ('OrganizationRequestSubmittedState','OrganizationRequestRejectedState','OrganizationRequestedForAdditionalInfoState','OrganizationRequestForCreateState','OrganizationRequestForUpdateState')--,'OrganizationRequestRejectedState')
	 OR ( stateid IN ('OrganizationRequestApprovedForUpdate') AND  isnull(IsUpdated,0)<>'1')-- checking if the update has been done by MCSupport after update approval
	 )
	 )
	 BEGIN
	 DECLARE @OrganizationUpdateRequestCurrentlyActive varchar='0'
	 IF  exists(select 1 from etrade.organizationrequests 
	 where OrganizationId = @OrgId    AND requestnumber LIKE '%OR/%' AND--Checking only organization request stateid of any update uncompleted request present
	 StateId in ('OrganizationRequestApprovedForUpdate','OrganizationRequestSubmittedState','OrganizationRequestRejectedState','OrganizationRequestedForAdditionalInfoState','OrganizationRequestForCreateState','OrganizationRequestForUpdateState')--,'OrganizationRequestRejectedState')
	 
	 )
	 BEGIN
	 SET @OrganizationUpdateRequestCurrentlyActive='1'
	 end
	 PRINT '@OrganizationUpdateRequestCurrentlyActive'

	 SELECT 0 'CanCreateRequest',@OrganizationUpdateRequestCurrentlyActive 'OrganizationUpdateRequestCurrentlyActive','RequestAccess' TableName-- show message to go to see submitted active request 
		return;
	 END
	 ELSE
	 BEGIN
--	 DECLARE @AuthoriedSignatoryListCount int=(SELECT count(*) FROM OrgAuthorizedSignatories WHERE organizationid=@OrgId AND stateid='')
--if(@AuthoriedSignatoryListCount>0)
--BEGIN
-- SELECT -1 'CanCreateRequest','RequestAccess' TableName-- show message say AuthSignatory table has no records for the organization to show for renewal and remove , but it should allow for adding new 
--		return;
--END
	 PRINT '@OrganizationUpdateRequestCurrentlyActiveNOT'
	 SELECT 1 'CanCreateRequest','RequestAccess' TableName-- allow to create or edit pending request
	 END


DECLARE @EServiceRequestIdParam VARCHAR(100)

IF not EXISTS (                
     SELECT 1                
     FROM [etrade].[EServiceRequests]                
     WHERE-- etrade.EServiceRequests.EServiceRequestNumber = @RequestNumber AND 
	 etrade.EServiceRequests.RequesterUserId=@UserId AND etrade.EServiceRequests.ServiceId=@serviceid
	 --AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGCreatedState','EServiceRequestORGRejectedState')--'EServiceRequestSubmittedState','EServiceRequestReSubmittedState',  -- create when there is no active request or no ongoing request            
 	 AND etrade.EServiceRequests.StateId IN ('EServiceRequestORGCreatedState','EServiceRequestORGForAdditionalInfo','EServiceRequestORGRejectedState') --'EServiceRequestSubmittedState','EServiceRequestReSubmittedState',  -- create when there is no active request or no ongoing request            
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
    ,null --qasem23-4            
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
              
 	Declare @Email varchar(150)=''
	SELECT @Email=EmailId FROM ETRADE.MobileUser WHERE UserId=@Userid
				    
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
	 --,AuthorizedPerson
	 --,CivilId          
  --   --,RequestForUserType,CivilID,LicenseNumber,UTFBrokerPersonalId
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
	,@Email              
  --  ,isnull( (SELECT a.EMAILID  FROM users a inner join   Organizations b on a.OrganizationId=b.OrganizationId
	 --WHERE a.OrganizationId  =@OrgId),                
  --  (SELECT  EmailId FROM ETRADE.MobileUser WHERE UserId=@Userid))                
     ,@serviceid                
     ,@Userid
 ,(
	 SELECT a.name  FROM organizations a 
  WHERE  a.ORGANIZATIONID=@OrgId)
  , (
	 SELECT b.Name  FROM organizations a INNER JOIN organizations_ara b ON a.OrganizationId=b.OrganizationId 
  WHERE  a.ORGANIZATIONID=@OrgId)
  ,(SELECT a.TradeLicenseNumber  FROM organizations a INNER JOIN organizations_ara b ON a.OrganizationId=b.OrganizationId 
  WHERE  a.ORGANIZATIONID=@OrgId)
	 ----,@BrokerTypeId,@civilIdvalue,@licenseNovalue,@BrokerPersonalId
	 --,@AuthorizedPerson
	 --,@CivilID
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
        
 INSERT INTO ETRADE.[$EServiceRequestsDetails] ([$AuditTrailId], [$UserId], [$Operation], [$DateTime], [$DataProfileClassId], [$IPId], [$SessionId], [EserviceRequestDetailsId], [EserviceRequestId], [RequestForUserType], [RequestServicesId], [OrganizationId], [RequesterLicenseNumber], [RequesterArabicName], [RequesterEnglishName], [ArabicFirstName], [ArabicSecondName], [ArabicThirdName], [ArabicLastName], [EnglishFirstName], [EnglishSecondName], [EnglishThirdName], [EnglishLastName], [Nationality], [CivilID], [CivilIDExpiryDate], [MobileNumber], [PassportNo], [PassportExpiryDate], [Address], [Email], [LicenseNumber], [LicenseNumberExpiryDate], [Remarks], [RejectionRemarks], [RequestForVisit], [RequestForVisitRemarks], [ExamAddmissionId], [ExamDetailsId], [status], [StateId], [DateCreated], [CreatedBy], [ModifiedBy], [DateModified], [OwnerOrgId], [OwnerLocId], [RequesterUserId], [RequestForName], [RequestForEmail], [Gender], [RequestForUserId], [BrokFileNo], [AssociatedOrgIds])        
 Select NEWID(), isnull(@Userid,@Userid), 1, GETDATE(), 'EServiceRequestsDetails', '127.0.0.1', 'system', [EserviceRequestDetailsId], [EserviceRequestId], [RequestForUserType], [RequestServicesId], [OrganizationId], [RequesterLicenseNumber], [RequesterArabicName], [RequesterEnglishName], [ArabicFirstName], [ArabicSecondName], [ArabicThirdName], [ArabicLastName], [EnglishFirstName], [EnglishSecondName], [EnglishThirdName], [EnglishLastName], [Nationality], [CivilID], [CivilIDExpiryDate], [MobileNumber], [PassportNo], [PassportExpiryDate], [Address], [Email], [LicenseNumber], [LicenseNumberExpiryDate], [Remarks], [RejectionRemarks], [RequestForVisit], [RequestForVisitRemarks], [ExamAddmissionId], [ExamDetailsId], [status], [StateId], [DateCreated], [CreatedBy], [ModifiedBy], [DateModified], [OwnerOrgId], [OwnerLocId], [RequesterUserId], [RequestForName], [RequestForEmail], [Gender], [RequestForUserId], [BrokFileNo], [AssociatedOrgIds] from etrade.EServiceRequestsDetails WHERE EServiceRequestId = @ForeignEserviceid--@EServiceRequestId         
        
        
/* audit entries for eserviceRequest and eserviceRequestDetails - start */        
   
  ---Create REquest

  SELECT a.RequesterUserId 'UserId',a.EServiceRequestId,a.EServiceRequestNumber, b.oldorgengname,b.oldorgaraname,b.neworgengname,b.neworgaraname,
  b.LicenseNumber,b.OrganizationId,a.stateid,'1' UpdateRequest,'1' EditableRequest,
  FORMAT (b.issuedate, 'dd-MM-yyyy') 'issuedate',FORMAT (b.expirydate, 'dd-MM-yyyy') 'expirydate',
 FORMAT (b.AuthorizedSignatoryCivilIdExpiryDate, 'dd-MM-yyyy') 'AuthorizedSignatoryCivilIdExpiryDate'  ,b.nationality
  ,B.AUTHORIZEDPERSON,B.CIVILID,
  b.AuthorizerIssuer As AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025
  ,'RequestDetails' TableName FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId
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
	@ADDCommercialLicenseNo nvarchar(200) =NULL,
	@ADDCommercialLicenseType int =NULL,
	@ADDCommercialLicenseIssueDate datetime =NULL,
	@ADDCommercialLicenseExpiryDate datetime =NULL,
	@ADDImporterLicenseNo nvarchar(200) =NULL,
	@ADDImporterLicenseType int =NULL,
	@ADDImporterLicenseIssueDate datetime =NULL,
	@ADDImporterLicenseExpiryDate datetime =NULL,
	@ADDIndustrialLicenseNo varchar(50) =NULL,
	@ADDIndIssueDate datetime =NULL,
	@ADDIndExpiryDate datetime =NULL,
	@ADDDateCreated datetime =getdate(),
	@ADDCreatedBy varchar(35) =@userid,
	@ADDStateId varchar(50) ='EServiceRequestORGCreatedState',
	@ADDRequestedDate datetime =NULL,
	@ADDEserviceRequestNumber varchar(100) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@ForeignEserviceid ),
	@AddAuthPersonNationality nvarchar(100) =NULL,
	@AddAuthPersonIssueDate nvarchar(100) =NULL,
	@AddAuthPersonExpiryDate nvarchar(100) =NULL,

	@ADDPostBoxNumber nvarchar(30) =NULL,
	@ADDCity nvarchar(60) =NULL,
	@ADDState nvarchar(60) =NULL,
	@ADDPostalCode nvarchar(30) =NULL,
	@ADDCountry int =NULL,
	@ADDAddress nvarchar(1000) =NULL,
	@ADDBlock nvarchar(200) =NULL,
	@ADDStreet nvarchar(200) =NULL,
	@ADDFloor                  NVARCHAR(200) = NULL, 
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
	@AddAuthPersonNationality  ,
	@AddAuthPersonIssueDate  ,
	@AddAuthPersonExpiryDate  ,

	@ADDPostBoxNumber  ,
	@ADDCity  ,
	@ADDState  ,
	@ADDPostalCode  ,
	@ADDCountry  ,
	@ADDAddress  ,
	@ADDBlock  ,
	@ADDStreet  ,
	@ADDFloor    , 
	@ADDApartmentType  ,
	@ADDApartmentNumber  ,



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
  b.LicenseNumber,b.OrganizationId,a.stateid,'0' UpdateRequest,'0' EditableRequest,--b.issuedate,b.expirydate,
  FORMAT (b.issuedate, 'dd-MM-yyyy') 'issuedate',FORMAT (b.expirydate, 'dd-MM-yyyy') 'expirydate'
  , FORMAT (b.AuthorizedSignatoryCivilIdExpiryDate, 'dd-MM-yyyy') 'AuthorizedSignatoryCivilIdExpiryDate'
  ,b.nationality
  ,B.AUTHORIZEDPERSON,B.CIVILID,
  B.AuthorizerIssuer As AuthorizerIssuerId, -- rama for authorizer issuer change 16-01-2025
  'RequestDetails' TableName  FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId
  WHERE a.RequesterUserId=@UserId AND a.ServiceId=@serviceid AND a.StateId IN ('EServiceRequestORGForAdditionalInfo','EServiceRequestORGCreatedState','EServiceRequestORGRejectedState')--'EServiceRequestORGRejectedState',
  
  SET @EServiceRequestIdParam=(select a.EServiceRequestId  FROM etrade.EServiceRequests a INNER JOIN etrade.EServiceRequestsDetails b ON a.EServiceRequestId=b.EserviceRequestId
  WHERE a.RequesterUserId=@UserId AND a.ServiceId=@serviceid  AND a.StateId IN ('EServiceRequestORGForAdditionalInfo','EServiceRequestORGCreatedState','EServiceRequestORGRejectedState'))--'EServiceRequestORGRejectedState',

  END

  IF(@serviceid=47 OR @serviceid=48 )
  BEGIN
  --SELECT * FROM OrgAuthorizedSignatories WHERE etrade.OrgAuthorizedSignatories.OrganizationId=@OrgId
  --AND stateid=''
  SELECT a.CivilID,a.AuthorizedPerson,a.AuthSignatoryId,a.OrganizationId,a.StateId,a.Sync,a.isActive,nationality,-- rama to add nationality
  FORMAT (convert(datetime, convert(date, a.AuthPersonIssueDate, 103), 103), 'dd-MM-yyyy') 'AuthPersonIssueDate',
  FORMAT (convert(datetime, convert(date, a.AuthPersonExpiryDate, 103), 103), 'dd-MM-yyyy') 'AuthPersonExpiryDate',
   FORMAT (convert(datetime, convert(date, a.AuthorizedSignatoryCivilIdExpiryDate, 103), 103), 'dd-MM-yyyy') 'AuthorizedSignatoryCivilIdExpiryDate',
  --convert(datetime, convert(date, a.AuthPersonIssueDate, 103), 103) 'AuthPersonIssueDate',
  --convert(datetime, convert(date, a.AuthPersonExpiryDate, 103), 103) 'AuthPersonExpiryDate',
  a.AuthorizerIssuer as AuthorizerIssuerId, -- rama for authorizer issuer change 16-01-2025
  'AuthorizedSignatories' TableName
   FROM OrgAuthorizedSignatories a INNER JOIN organizations b -- etrade.EServiceRequestsDetails b
  ON a.OrganizationId = b.OrganizationId --AND a.CivilID = b.CivilID
   WHERE  a.OrganizationId=@OrgId AND a.stateid ='OrgAuthorizedSignatoriesActivatedState'--AND b.RequestServicesId=@serviceid
   --AND (b.StateId NOT IN ('EServiceRequestSubmittedState','EServiceRequestReSubmittedState',
   --'EServiceRequestCreatedState','EServiceRequestRejectedState') OR b.EserviceRequestId= @EServiceRequestIdParam)
   
   --UPDATE etrade.OrgAuthorizedSignatories SET organizationid='732857'

  END

    --exec etrade.GetOrgLicenseDocuments @LanguageId,'M',@EServiceRequestIdParam

	DECLARE @RefProfile nvarchar(200)=null
IF(@serviceid=46)
  set @RefProfile='EserviceAuthorizedSignatoryDocs'
  ELSE IF (@serviceid=47)
  set @RefProfile='EserviceAuthorizedSignatoryDocs'
  ELSE IF (@serviceid=48)
  set @RefProfile='EserviceAuthorizedSignatoryDocs'
  
exec [etrade].[getreqdocsddldataforeservices] @EServiceRequestIdParam,@LanguageId,@RefProfile  
EXEC [etrade].[usp_GetUploadedDocumentsInfoForeservices] @LanguageId,'DocumentId','ScanRequestUploadDocs','M',@EServiceRequestIdParam,'',@RefProfile,''

 end  
