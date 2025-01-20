USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[SubmitOrgNameChangeRequest]    Script Date: 1/16/2025 4:48:25 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER    PROC [etrade].[SubmitOrgNameChangeRequest]
(@UserId varchar(100)
--, @OrgId nvarchar(100)=null
--,@OldOrgEngName nvarchar(200)=null
--, @OldOrgAraName nvarchar(200)=null
, @NewOrgEngName nvarchar(200)=null
, @NewOrgAraName nvarchar(200)=null
, @RequestNumber nvarchar(200)=null
, @AuthorizerCivilIdExpiryDate Varchar(50)=null
, @AuthorizerIssueDate Varchar(50)=null
,@AuthorizerExpiryDate Varchar(50)=null
,@AuthorizerCivilId Varchar(50)=null,
 @AuthorizerIssuerId nvarchar(50) =null -- rama for authorizer issuer change 16-01-2025
 ,@AuthorizerNationality nvarchar(50) =null -- rama for authorizer issuer change 16-01-2025
 
)
AS
BEGIN
 
 Declare @EserviceRequestId int 
  DECLARE @serviceid int =(SELECT ServiceID FROM etrade.Eservices                
    WHERE ServiceNameEng=N'	Org Name Change')  

		DECLARE @OrgId nvarchar(200)
SET  @OrgId=(SELECT top 1 a.ORGANIZATIONID FROM etrade.mobileuserorgmaps a INNER JOIN etrade.mobileuser b 
on  a.userid=b.userid AND a.ParentOrgTradeLicence=b.LicenseNumber WHERE a.userid=@UserId ORDER BY a.Mobileuserorgmapid desc)--(SELECT ORGANIZATIONID FROM etrade.mobileuserorgmaps WHERE userid=@UserId )--AND etrade.mobileuserorgmaps.isActive=1)

  
  SET @EserviceRequestId = (SELECT EserviceRequestId FROM ETRADE.ESERVICEREQUESTS WHERE EServiceRequestNumber=@RequestNumber)
  --SELECT * FROM [etrade].[EServiceRequests]  WHERE
  -- etrade.EServiceRequests.EServiceRequestNumber = @RequestNumber AND etrade.EServiceRequests.RequesterUserId=@UserId

 IF EXISTS(SELECT 1 FROM ETRADE.EServiceRequests A INNER JOIN ETRADE.EServiceRequestsDetails B ON A.EServiceRequestId=B.EserviceRequestId
WHERE A.ServiceId=@serviceid AND B.OrganizationId=@OrgId
AND A.StateId  IN('EServiceRequestORGSubmittedState','EServiceRequestORGForAdditionalInfo'
,'EServiceRequestORGForVisitState','EServiceRequestORGReSubmittedState')   AND a.eServiceRequestId!=@eServiceRequestId)
BEGIN
SELECT '-1111' AS 'STATUS'
RETURN;
END              

   UPDATE etrade.EServiceRequests 
   SET
       etrade.EServiceRequests.RequestSubmissionDateTime = GETDATE(), -- datetime
       etrade.EServiceRequests.StateId = (CASE WHEN b.rejectionremarks IS NULL then  'EServiceRequestORGSubmittedState' ELSE 'EServiceRequestORGReSubmittedState' end), -- varchar
      -- etrade.EServiceRequests.DateCreated =  GETDATE(), -- datetime  qasem23-4
       etrade.EServiceRequests.CreatedBy = @UserId, -- varchar
       etrade.EServiceRequests.DateModified = GETDATE() -- datetime
       --etrade.EServiceRequests.ServiceId = @serviceid, -- bigint
       --etrade.EServiceRequests.RequesterUserId = @UserId -- bigint
	   from etrade.EServiceRequestsDetails b

	   WHERE etrade.EServiceRequests.EserviceRequestId=@EserviceRequestId--38279 --@EserviceRequestId
	   AND b.EserviceRequestId=@EserviceRequestId

	   UPDATE etrade.EServiceRequestsDetails
	   SET
	       etrade.EServiceRequestsDetails.RequesterLicenseNumber = N'', -- nvarchar
	       etrade.EServiceRequestsDetails.RequesterArabicName = N'', -- nvarchar
	       etrade.EServiceRequestsDetails.RequesterEnglishName = N'', -- nvarchar
	       etrade.EServiceRequestsDetails.NewOrgEngName = @NewOrgEngName, -- nvarchar
	       etrade.EServiceRequestsDetails.NewOrgAraName = @NewOrgAraName, -- nvarchar
	       etrade.EServiceRequestsDetails.status = '', -- varchar
	       etrade.EServiceRequestsDetails.StateId =(CASE WHEN rejectionremarks IS NULL then  'EServiceRequestDetailsORGSubmittedState' ELSE 'EServiceRequestDetailsORGReSubmittedState' end) -- varchar
		   ,etrade.EServiceRequestsDetails.ReadyForSahelSubmission=0
		   ,etrade.EServiceRequestsDetails.IssueDate =@AuthorizerIssueDate
		   ,etrade.EServiceRequestsDetails.ExpiryDate = @AuthorizerExpiryDate
		   ,etrade.EServiceRequestsDetails.AuthorizedSignatoryCivilIdExpiryDate = @AuthorizerCivilIdExpiryDate
		   ,etrade.EServiceRequestsDetails.CivilID = @AuthorizerCivilId
		   ,etrade.EServiceRequestsDetails.AuthorizerIssuer = @AuthorizerIssuerId  -- rama for authorizer issuer change 16-01-2025
		    ,etrade.EServiceRequestsDetails.Nationality = @AuthorizerNationality-- rama for authorizer issuer change 16-01-2025
		   WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@EserviceRequestId

		   
  
  Declare @ADDOrganizationRequestId int =(SELECT OrganizationRequestId FROM etrade.OrganizationRequests WHERE RequestNumber=(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId )),
	@ADDRequestNumber nvarchar(50) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId ),
	@ADDRequesterOwner int =@userid,
	@ADDName nvarchar(240) =@NewOrgEngName,
	@ADDOrganizationId int =@orgid,
	@ADDLocalDescription nvarchar(340) =@NewOrgAraName,
  @ADDCivilId varchar(20) =@AuthorizerCivilId,
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
	@ADDStateId varchar(50) ='EServiceRequestORGSubmittedState',
	@ADDRequestedDate datetime =NULL,
	@ADDEserviceRequestNumber varchar(100) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId ),
	@AuthPersonNationality nvarchar(100) =@AuthorizerNationality,-- rama for authorizer issuer change 16-01-2025
	  @AuthPersonIssueDate nvarchar(100) =@AuthorizerIssueDate,
   @AuthPersonExpiryDate nvarchar(100) =@AuthorizerExpiryDate,


	@ADDPostBoxNumber nvarchar(30) =NULL,
	@ADDCity nvarchar(60) =NULL,
	@ADDState nvarchar(60) =NULL,
	@ADDPostalCode nvarchar(30) =NULL,
	@ADDCountry int =NULL,
	@ADDAddress nvarchar(1000) =NULL,
	@ADDBlock nvarchar(200) =NULL,
	@ADDStreet nvarchar(200) =NULL,
	@Floor                  NVARCHAR(200) = NULL, 
	@ADDApartmentType nvarchar(200) =NULL,
	@ADDApartmentNumber nvarchar(200) =NULL,



	@ADDBusinessTelNumber nvarchar(40) =NULL,
	@ADDHomeTelNumber varchar(20) =NULL,
	@ADDBusinessFaxNumber nvarchar(40) =NULL,
	@ADDMobileTelNumber varchar(15) =NULL,
	@ADDEMail varchar(50) =NULL,
	@ADDWebPageAddress varchar(50) =NULL,
	@ADDType nvarchar(50)='Submit' --Start/Submit
	 ,@ADDAuthorizerIssuerId varchar(50) =@AuthorizerIssuerId-- rama for authorizer issuer change 16-01-2025

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
	@AuthPersonNationality  ,
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
	@Floor   , 
	@ADDApartmentType ,
	@ADDApartmentNumber ,



	@ADDBusinessTelNumber  ,
	@ADDHomeTelNumber  ,
	@ADDBusinessFaxNumber  ,
	@ADDMobileTelNumber  ,
	@ADDEMail  ,
	@ADDWebPageAddress  ,
	@ADDType  --Start/Submit
	,@ADDAuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025

 
 DECLARE @EmailId VARCHAR(100)    
    
  SELECT @EmailId = EmailId    
  FROM etrade.MobileUSer    
  WHERE UserId = @UserId  
 
  INSERT INTO [KGACEmailOutSyncQueue] (    
   [Sync]    
   ,[MsgType]    
   ,[UserId]    
   ,[TOEmailAddress]    
   ,[CCEmailAddress]    
   ,[BCCEmailAddress]    
   ,[SampleRequestNo]    
   ,[DateCreated]    
   ,[DateModified]    
   ,[Status]    
   ,ManifestNo    
   )    
  VALUES (    
   0    
   ,'OrganizationNameChangeReqSubmit'    
   ,''    
   ,@EmailId    
   ,''    
   ,''    
   ,(SELECT OrganizationRequestId FROM etrade.OrganizationRequests WHERE RequestNumber=(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId ))--@EserviceRequestId    
   ,GETDATE()    
   ,GETDATE()    
   ,'Created'    
   ,null    
   )    
       
    
     
 --exec USP_Eservices_MailNotification 0,0,0,'',1,@EserviceRequestId    
      
	  --INSERT INTO KGACSMSOutQueue(SYNC,MSGTYPE,TOMOBILENO,REFERENCETYPE,REFERENCEID,REFERENCENO,SMSMESSAGE,DATECREATED)
	  --VALUES(0,NULL,(SELECT MOBILENUMBER FROM ETRADE.MobileUser WHERE USERID=@UserId),NULL,NULL,NULL,N'',GETDATE())
	

SELECT 1 'Status'

END
