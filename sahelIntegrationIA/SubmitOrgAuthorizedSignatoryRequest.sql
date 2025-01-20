USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[SubmitOrgAuthorizedSignatoryRequest]    Script Date: 1/16/2025 1:34:00 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

 ALTER   PROC [etrade].[SubmitOrgAuthorizedSignatoryRequest]  
 (  
 @RequestNumber nvarchar(200)=NULL  
,@eServiceRequestId nvarchar(200)=NULL  
--, @TypeOfLicenseRequest nvarchar(200)=NULL--ImporterLicenseRequest/IndustrialLicenseRequest/CommercialLicenseRequest  
--,@serviceid int =NULL  
,@AuthorizedPerson nvarchar(200)=NULL,    
  @CivilID nvarchar(200)=NULL  ,  
  
  @Nationality nvarchar(200)=NULL,  
  @IssueDate nvarchar(200) =null,  
  @ExpiryDate nvarchar(200) =null,  
  @AuthorizedSignatoryCivilIdExpiryDate nvarchar(200) =null  ,
  @AuthorizerIssuerId nvarchar(50) =null -- rama for authorizer issuer change 16-01-2025
 )  
 as  
 BEGIN  
 DECLARE @UserId int =(SELECT requesteruserid FROM etrade.EServiceRequests WHERE EServiceRequestId = @eServiceRequestId)  
  
  DECLARE @OrgId nvarchar(200)  
SET  @OrgId=(SELECT top 1 a.ORGANIZATIONID FROM etrade.mobileuserorgmaps a INNER JOIN etrade.mobileuser b   
on  a.userid=b.userid AND a.ParentOrgTradeLicence=b.LicenseNumber WHERE a.isActive=1 and 
a.userid=@UserId ORDER BY a.mobileuserorgmapid desc)--(SELECT ORGANIZATIONID FROM etrade.mobileuserorgmaps WHERE userid=@UserId )--AND etrade.mobileuserorgmaps.isActive=1)  
  
 DECLARE @serviceid int=null  
    
  SELECT @serviceid=serviceid from etrade.EServiceRequests er WHERE EServiceRequestId=@EserviceRequestId  
  
  --check if same data active for the same service request or same data active in org or same data used in renew or remove at the same time  
  
IF EXISTS(SELECT 1 FROM ETRADE.EServiceRequests A INNER JOIN ETRADE.EServiceRequestsDetails B ON A.EServiceRequestId=B.EserviceRequestId  
WHERE A.ServiceId=@serviceid AND   
(B.CivilID=@CivilID ) AND B.OrganizationId=@OrgId  
AND A.StateId IN('EServiceRequestORGSubmittedState','EServiceRequestORGForAdditionalInfo'  
,'EServiceRequestORGForVisitState','EServiceRequestORGReSubmittedState') AND a.eServiceRequestId!=@eServiceRequestId)  
OR (@serviceid=46 AND  EXISTS(SELECT 1 FROM dbo.OrgAuthorizedSignatories  WHERE   
ORGANIZATIONID=@OrgId AND civilid=@CivilID AND   STATEID='OrgAuthorizedSignatoriesActivatedState' )   )  
OR EXISTS(SELECT 1 FROM ETRADE.EServiceRequests A INNER JOIN ETRADE.EServiceRequestsDetails B ON A.EServiceRequestId=B.EserviceRequestId  
WHERE (A.ServiceId=@serviceid OR a.ServiceId=47) AND   
(B.CivilID=@CivilID ) AND B.OrganizationId=@OrgId  
AND A.StateId IN('EServiceRequestORGSubmittedState','EServiceRequestORGForAdditionalInfo'  
,'EServiceRequestORGForVisitState','EServiceRequestORGReSubmittedState') AND a.eServiceRequestId!=@eServiceRequestId )  
OR EXISTS(SELECT 1 FROM ETRADE.EServiceRequests A INNER JOIN ETRADE.EServiceRequestsDetails B ON A.EServiceRequestId=B.EserviceRequestId  
WHERE (A.ServiceId=@serviceid OR a.ServiceId=48) AND   
(B.CivilID=@CivilID ) AND B.OrganizationId=@OrgId  
AND A.StateId IN('EServiceRequestORGSubmittedState','EServiceRequestORGForAdditionalInfo'  
,'EServiceRequestORGForVisitState','EServiceRequestORGReSubmittedState') AND a.eServiceRequestId!=@eServiceRequestId )  
OR EXISTS(  SELECT 1 FROM etrade.EServiceRequests  WHERE   
serviceid=41 AND createdby=@userid AND    STATEID IN('EServiceRequestORGSubmittedState','EServiceRequestORGForAdditionalInfo'  
,'EServiceRequestORGForVisitState','EServiceRequestORGReSubmittedState')  )  
BEGIN  
SELECT '-1111' AS 'STATUS'  
RETURN;  
END  
  
  
DECLARE @ExistingSignatoriesCount int =(SELECT count(*) FROM dbo.OrgAuthorizedSignatories  WHERE   
ORGANIZATIONID=@OrgId   AND   STATEID='OrgAuthorizedSignatoriesActivatedState')  
  
IF (@ExistingSignatoriesCount=1 AND @serviceid=48)  
BEGIN  
SELECT '-2222' AS 'STATUS'  
RETURN;  
END  
  
  
 UPDATE etrade.EServiceRequests  
 SET  
     etrade.EServiceRequests.RequestSubmissionDateTime = getdate(), -- datetime  
     etrade.EServiceRequests.StateId = (CASE WHEN b.RejectionRemarks IS NULL then  'EServiceRequestORGSubmittedState' ELSE 'EServiceRequestORGReSubmittedState' end), -- varchar  
     etrade.EServiceRequests.DateModified = getdate()--, -- datetime  
  from etrade.EServiceRequestsDetails b  
  
    WHERE etrade.EServiceRequests.EserviceRequestId=@EserviceRequestId--38279 --@EserviceRequestId  
    AND b.EserviceRequestId=@EserviceRequestId  
  
    --SELECT @IssueDate=isnull(@IssueDate,AuthPersonIssueDate) , @ExpiryDate=isnull(@ExpiryDate,AuthPersonExpiryDate),  
    --@Nationality=isnull(@Nationality,Nationality)  
    --FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    --(SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    --AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'  
  
    SET @IssueDate=isnull(@IssueDate,(select-- isnull(@IssueDate,AuthPersonIssueDate)  
    AuthPersonIssueDate  
    FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    (SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'))  
    SET @ExpiryDate=isnull(@ExpiryDate,(select  AuthPersonExpiryDate-- isnull(@ExpiryDate,AuthPersonExpiryDate)   
    FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    (SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'))  
    SET @AuthorizedSignatoryCivilIdExpiryDate=isnull(@AuthorizedSignatoryCivilIdExpiryDate,(select  AuthorizedSignatoryCivilIdExpiryDate-- isnull(@ExpiryDate,AuthPersonExpiryDate)   
    FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    (SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'))  
    SET @Nationality=isnull(@Nationality,(select    
    Nationality--isnull(@Nationality,Nationality)  
    FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    (SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'))  
     SET @AuthorizedPerson=isnull(@AuthorizedPerson,(select    
    AuthorizedPerson--isnull(@Nationality,Nationality)  
    FROM dbo.OrgAuthorizedSignatories  WHERE dbo.OrgAuthorizedSignatories.OrganizationId=  
    (SELECT Organizationid FROM etrade.EServiceRequestsDetails WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId)  
    AND dbo.OrgAuthorizedSignatories.CivilID=@CivilID AND stateid='OrgAuthorizedSignatoriesActivatedState'))  
    --SELECT * FROM OrgAuthorizedSignatories  
  --PRINT '@AuthorizedSignatoryCivilIdExpiryDate 1'
  --PRINT @AuthorizedSignatoryCivilIdExpiryDate-- this field alone is datetime2 datatyoe in db so need to handle explicityly
  --SET @AuthorizedSignatoryCivilIdExpiryDate= convert(datetime,convert(datetime2,@AuthorizedSignatoryCivilIdExpiryDate ))
  --PRINT @IssueDate
  --PRINT @ExpiryDate
  --PRINT @serviceid
  SET @IssueDate=case when @serviceid=48 then @IssueDate ELSE convert(datetime, convert(date, @IssueDate, 103), 103) END
  --PRINT @IssueDate
  SET @ExpiryDate=case when @serviceid=48 then @ExpiryDate ELSE convert(datetime, convert(date, @ExpiryDate, 103), 103) end
  --PRINT @ExpiryDate
  --PRINT '@before' +@AuthorizedSignatoryCivilIdExpiryDate

  --SET @AuthorizedSignatoryCivilIdExpiryDate=case when @serviceid=48 then @AuthorizedSignatoryCivilIdExpiryDate ELSE convert(datetime, convert(date, @AuthorizedSignatoryCivilIdExpiryDate, 103), 103) end
    SET @AuthorizedSignatoryCivilIdExpiryDate=case when @serviceid=48 then @AuthorizedSignatoryCivilIdExpiryDate 
        ELSE convert(datetime2, convert(date, @AuthorizedSignatoryCivilIdExpiryDate, 103), 103) end

  --PRINT '@AuthorizedSignatoryCivilIdExpiryDate' +@AuthorizedSignatoryCivilIdExpiryDate

  UPDATE etrade.EServiceRequestsDetails  
  SET  
  
      etrade.EServiceRequestsDetails.status = '', -- varchar  
      etrade.EServiceRequestsDetails.StateId = (CASE WHEN RejectionRemarks IS NULL then  'EServiceRequestDetailsORGSubmittedState' ELSE 'EServiceRequestDetailsORGReSubmittedState' end), -- varchar  
      etrade.EServiceRequestsDetails.DateModified = getdate(), -- datetime  
      etrade.EServiceRequestsDetails.AuthorizedPerson = @AuthorizedPerson, -- nvarchar  
      etrade.EServiceRequestsDetails.CivilID = @CivilID,  
      etrade.EServiceRequestsDetails.Nationality = @Nationality, -- nvarchar  
      etrade.EServiceRequestsDetails.IssueDate =@IssueDate,--convert(datetime, convert(date, @IssueDate, 103), 103) , -- nvarchar  
      etrade.EServiceRequestsDetails.ExpiryDate =@ExpiryDate,--convert(datetime, convert(date, @ExpiryDate, 103), 103),  -- nvarchar  
      etrade.EServiceRequestsDetails.AuthorizedSignatoryCivilIdExpiryDate =@AuthorizedSignatoryCivilIdExpiryDate--convert(datetime, convert(date, @AuthorizedSignatoryCivilIdExpiryDate, 103), 103)  -- nvarchar  
	  ,	   etrade.EServiceRequestsDetails.ReadyForSahelSubmission=0
	  , etrade.EServiceRequestsDetails.AuthorizerIssuer = @AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025
   WHERE etrade.EServiceRequestsDetails.EserviceRequestId=@eServiceRequestId  
     
       
    
  Declare @ADDOrganizationRequestId int =(SELECT OrganizationRequestId FROM etrade.OrganizationRequests WHERE RequestNumber=(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId )),  
 @ADDRequestNumber nvarchar(50) =(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId ),  
 @ADDRequesterOwner int =@userid,  
 @ADDName nvarchar(240) =NULL,  
 @ADDOrganizationId int =@orgid,  
 @ADDLocalDescription nvarchar(340) =NULL,  
 @ADDCivilId varchar(20) =@CivilID,  
 @ADDAuthPerson nvarchar(240) =@AuthorizedPerson,  
 @ADDCommercialLicenseNo nvarchar(200) =null,  
 @ADDCommercialLicenseType int =NULL,  
 @ADDCommercialLicenseIssueDate datetime =NULL,  
 @ADDCommercialLicenseExpiryDate datetime =NULL,  
 @ADDImporterLicenseNo nvarchar(200) =null,  
 @ADDImporterLicenseType int =NULL,  
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
 @AddAuthPersonNationality nvarchar(100) =@Nationality,  
 @AddAuthPersonIssueDate nvarchar(100) =@IssueDate,--convert(datetime, convert(date, @IssueDate, 103), 103),  
 @AddAuthPersonExpiryDate nvarchar(100) =@ExpiryDate,--convert(datetime, convert(date, @ExpiryDate, 103), 103),  
  
 @ADDPostBoxNumber nvarchar(30) =NULL,  
 @ADDCity nvarchar(60) =NULL,  
 @ADDState nvarchar(60) =NULL,  
 @ADDPostalCode nvarchar(30) =NULL,  
 @ADDCountry int =@Nationality,  
 @ADDAddress nvarchar(1000) =NULL,  
 @ADDBlock nvarchar(200) =NULL,  
 @ADDStreet nvarchar(200) =NULL,  
 @ADDFloor nvarchar(200) =NULL,  
 @ADDApartmentType nvarchar(200) =NULL,  
 @ADDApartmentNumber nvarchar(200) =NULL,  
  
  
  
 @ADDBusinessTelNumber nvarchar(40) =NULL,  
 @ADDHomeTelNumber varchar(20) =NULL,  
 @ADDBusinessFaxNumber nvarchar(40) =NULL,  
 @ADDMobileTelNumber varchar(15) =NULL,  
 @ADDEMail varchar(50) =NULL,  
 @ADDWebPageAddress varchar(50) =NULL,  
 @ADDType nvarchar(50)='Submit' --Start/Submit  
 --,@ADDeservicerequestid int =@eServiceRequestId 
 ,@ADDAuthorizerIssuerId varchar(50) =@AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025
  
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
 @ADDFloor  ,  
 @ADDApartmentType  ,  
 @ADDApartmentNumber  ,  
  
  
  
 @ADDBusinessTelNumber  ,  
 @ADDHomeTelNumber  ,  
 @ADDBusinessFaxNumber  ,  
 @ADDMobileTelNumber  ,  
 @ADDEMail  ,  
 @ADDWebPageAddress  ,  
 @ADDType  --Start/Submit  
 --,@ADDeservicerequestid  
  ,@ADDAuthorizerIssuerId
     
   DECLARE @EmailId VARCHAR(100)      
      
  SELECT @EmailId = EmailId      
  FROM etrade.MobileUSer      
  WHERE UserId = @UserId    
  
   
  --SELECT * FROM etrade.eservices  
  DECLARE @TypeOfLicenseRequest nvarchar(200)=null  
  if(@serviceid=46)  
 SET @TypeOfLicenseRequest=N'AddNewAuthorizedSignatoryReqSubmit'  
   if(@serviceid=47)  
 SET @TypeOfLicenseRequest=N'RenewAuthorizedSignatoryReqSubmit'  
  if(@serviceid=48)  
 SET @TypeOfLicenseRequest=N'RemoveAuthorizedSignatoryReqSubmit'  
   
  
   
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
   ,@TypeOfLicenseRequest  
   ,''      
   ,@EmailId      
   ,''      
   ,''      
    ,(SELECT OrganizationRequestId FROM etrade.OrganizationRequests WHERE RequestNumber=(SELECT EServiceRequestNumber FROM etrade.EServiceRequests WHERE EServiceRequestId=@EserviceRequestId ))--,@EserviceRequestId      
   ,GETDATE()      
   ,GETDATE()      
   ,'Created'      
   ,null      
   )      
         
  
   SELECT '1' AS 'Status'  
  
 END
