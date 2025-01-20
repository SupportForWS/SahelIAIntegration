USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[ManageOrgServices]    Script Date: 1/16/2025 1:41:02 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



ALTER      PROC [etrade].[ManageOrgServices] (
@OrganizationRequestId int =null,
	--[OrganizationRequestId] [int] IDENTITY(1,1) NOT NULL,
	--[RequestType] [int] =NULL,
	@RequestNumber nvarchar(50) =NULL,
	@RequesterOwner int =NULL,
	@Name nvarchar(240) =NULL,
	@OrganizationId int =NULL,
	--@Description nvarchar(240) =NULL,
	--@TradeLicenseNumber nvarchar(30) =NULL,
	@LocalDescription nvarchar(340) =NULL,
	@CivilId varchar(20) =NULL,
	@AuthPerson nvarchar(240) =NULL,
	@CommercialLicenseNo nvarchar(200) =NULL,
	--@CommercialLicenseValidated int =NULL,
	@CommercialLicenseType int =NULL,
	@CommercialLicenseIssueDate datetime =NULL,
	@CommercialLicenseExpiryDate datetime =NULL,
	--@CommercialLicenseSubType int =NULL,
	--@ImporterLicValidated int =NULL,
	@ImporterLicenseNo nvarchar(200) =NULL,
	@ImporterLicenseType int =NULL,
	@ImporterLicenseIssueDate datetime =NULL,
	@ImporterLicenseExpiryDate datetime =NULL,
	--@IndustrialLicValidated int =NULL,
	--@IsIndustrial char(1) =NULL,
	@IndustrialLicenseNo varchar(50) =NULL,
	@IndIssueDate datetime =NULL,
	@IndExpiryDate datetime =NULL,
	--@IndRegNo varchar(50) =NULL,
	--@IndRegIssuanceDate dateime =NULL,
	@DateCreated datetime =NULL,
	@CreatedBy varchar(35) =NULL,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	@StateId varchar(50) =NULL,
	--@OrgReqDocType int =NULL,
	@RequestedDate datetime =NULL,
	--@RejectionRemarks nvarchar(500) =NULL,
	--@AdditionalInfoRemarks nvarchar(500) =NULL,
	--@RequestForVisitRemarks nvarchar(500) =NULL,
	--@IsMainCompany char(1) =NULL,
	--@ParentMainCompany int =NULL,
	@EserviceRequestNumber varchar(100) =NULL,
	--@ParentOrgTradeLicence nvarchar(150) =NULL,
	--@IsUpdated char(1) =NULL,
	--@ProcessedBy varchar(35) =NULL,
	--@ProcessedDate datetime =NULL,
	@AuthPersonNationality nvarchar(100) =NULL,
	@AuthPersonIssueDate nvarchar(100) =NULL,
	@AuthPersonExpiryDate nvarchar(100) =NULL,

	--@OrganizationRequestId int NOT =NULL,
	--@OrganizationRequestAddresseId int IDENTITY(1,1) NOT =NULL,
	@PostBoxNumber nvarchar(30) =NULL,
	@City nvarchar(60) =NULL,
	@State nvarchar(60) =NULL,
	@PostalCode nvarchar(30) =NULL,
	@Country int =NULL,
	@Address nvarchar(1000) =NULL,
	--@DateCreated datetime =NULL,
	--@CreatedBy varchar(35) =NULL,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	--@StateId varchar(50) =NULL,
	@Block nvarchar(200) =NULL,
	@Street nvarchar(200) =NULL,
@Floor                  NVARCHAR(200) = NULL,  
@ApartmentType                  NVARCHAR(200) = NULL,  
@ApartmentNumber                  NVARCHAR(200) = NULL ,
	
	--@OrganizationRequestContactId int IDENTITY(1,1) NOT =NULL,
	--@OrganizationRequestId int NOT =NULL,
	@BusinessTelNumber nvarchar(40) =NULL,
	@HomeTelNumber varchar(20) =NULL,
	@BusinessFaxNumber nvarchar(40) =NULL,
	@MobileTelNumber varchar(15) =NULL,
	@EMail varchar(50) =NULL,
	@WebPageAddress varchar(50) =NULL,
	--@DateCreated datetime =NULL,
	--@CreatedBy varchar(35) =NULL,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	--@StateId varchar(50) =NULL,
	--@IsEmailVarified int =NULL,
	--@IsMobileVarified int =NULL,
	@Type nvarchar(50)=NULL --Start/Submit
	--,@ADDeservicerequestid int =0
	,  @AuthorizerIssuerId nvarchar(50) =null -- rama for authorizer issuer change 16-01-2025
	)
	AS BEGIN

	DECLARE @EServiceRequestId int=(SELECT eservicerequestid FROM etrade.eservicerequests WHERE etrade.eservicerequests.EServiceRequestNumber
=( SELECT OReq.EserviceRequestNumber   
  FROM [etrade].OrganizationRequests OReq    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId) )

 DECLARE @ServiceId int =(SELECT serviceid FROM etrade.eservicerequests WHERE eservicerequestid=@EServiceRequestId)
 DECLARE @AuthorizedSignatoryCivilIdExpiryDate nvarchar(200) =null
 if(@ServiceId=46 OR @ServiceId=47 OR @ServiceId=48)
 BEGIN
 SET @AuthorizedSignatoryCivilIdExpiryDate=(SELECT AuthorizedSignatoryCivilIdExpiryDate FROM etrade.eservicerequestsdetails WHERE eservicerequestid=@EServiceRequestId)
 END


	if(@Type='Start')
	BEGIN
	SET @StateId='OrganizationRequestCreatedState'
	END
	ELSE
	BEGIN
	SET @StateId='OrganizationRequestSubmittedState'
	END

	if(@Type='Start')
	BEGIN

	  DECLARE @InsertedRow TABLE (Id INT) 
	     

	INSERT INTO [etrade].[OrganizationRequests]
           (
		   --[RequestType]
           [RequestNumber]
           ,[RequesterOwner]
           ,[Name]
           ,[OrganizationId]
           --,[Description]
           --,[TradeLicenseNumber]
           ,[LocalDescription]
           ,[CivilId]
           ,[AuthPerson]
           ,[CommercialLicenseNo]
           --,[CommercialLicenseValidated]
           --,[CommercialLicenseType]
           ,[CommercialLicenseIssueDate]
           ,[CommercialLicenseExpiryDate]
           --,[CommercialLicenseSubType]
           --,[ImporterLicValidated]
           ,[ImporterLicenseNo]
           --,[ImporterLicenseType]
           ,[ImporterLicenseIssueDate]
           ,[ImporterLicenseExpiryDate]
           --,[IndustrialLicValidated]
           --,[IsIndustrial]
           ,[IndustrialLicenseNo]
           ,[IndIssueDate]
           ,[IndExpiryDate]
           --,[IndRegNo]
           --,[IndRegIssuanceDate]
           ,[DateCreated]
           ,[CreatedBy]
           --,[DateModified]
           --,[ModifiedBy]
           --,[OwnerLocId]
           --,[OwnerOrgId]
           ,[StateId]
           --,[OrgReqDocType]
           --,[RequestedDate]
           --,[RejectionRemarks]
           --,[AdditionalInfoRemarks]
           --,[RequestForVisitRemarks]
           --,[IsMainCompany]
           --,[ParentMainCompany]
           ,[EserviceRequestNumber]
           --,[ParentOrgTradeLicence]
           --,[IsUpdated]
           --,[ProcessedBy]
           --,[ProcessedDate]
		   ,[AuthorizerIssuer] -- rama for authorizer issuer change 16-01-2025
		   )
		   OUTPUT inserted.OrganizationRequestId                                
  INTO @InsertedRow
     VALUES
           (
		  --[RequestType] [int] =NULL,
	@RequestNumber ,
	@RequesterOwner ,
	@Name ,
	@OrganizationId ,
	--@Description nvarchar(240) =NULL,
	--@TradeLicenseNumber nvarchar(30) =NULL,
	@LocalDescription ,
	@CivilId ,
	@AuthPerson ,
	@CommercialLicenseNo ,
	--@CommercialLicenseValidated int =NULL,
	--@CommercialLicenseType int =NULL,
	@CommercialLicenseIssueDate ,
	@CommercialLicenseExpiryDate ,
	--@CommercialLicenseSubType int =NULL,
	--@ImporterLicValidated int =NULL,
	@ImporterLicenseNo ,
	--@ImporterLicenseType int =NULL,
	@ImporterLicenseIssueDate ,
	@ImporterLicenseExpiryDate ,
	--@IndustrialLicValidated int =NULL,
	--@IsIndustrial char(1) =NULL,
	@IndustrialLicenseNo ,
	@IndIssueDate ,
	@IndExpiryDate ,
	--@IndRegNo varchar(50) =NULL,
	--@IndRegIssuanceDate dateime =NULL,
	GETDATE(),--@DateCreated ,
	@CreatedBy ,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	@StateId ,
	--@OrgReqDocType int =NULL,
	--@RequestedDate ,
	--@RejectionRemarks nvarchar(500) =NULL,
	--@AdditionalInfoRemarks nvarchar(500) =NULL,
	--@RequestForVisitRemarks nvarchar(500) =NULL,
	--@IsMainCompany char(1) =NULL,
	--@ParentMainCompany int =NULL,
	@EserviceRequestNumber 
	--@ParentOrgTradeLicence nvarchar(150) =NULL,
	--@IsUpdated char(1) =NULL,
	--@ProcessedBy varchar(35) =NULL,
	--@ProcessedDate datetime =NULL,
	,@AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025
		  )


		   
INSERT INTO [etrade].[OrganizationRequestAddresses]
           (
		   [OrganizationRequestId]
           ,[PostBoxNumber]
           ,[City]
           ,[State]
           ,[PostalCode]
           ,[Country]
           ,[Address]
           ,[DateCreated]
           ,[CreatedBy]
           --,[DateModified]
           --,[ModifiedBy]
           --,[OwnerLocId]
           --,[OwnerOrgId]
           ,[StateId]
           ,[Block]
           ,[Street])
     VALUES
           (
		 (select id from @InsertedRow) ,-- @OrganizationRequestId ,
	--@OrganizationRequestAddresseId int IDENTITY(1,1) NOT =NULL,
	@PostBoxNumber ,
	@City ,
	@State ,
	@PostalCode ,
	@Country ,
	@Address ,
	GETDATE(),--@DateCreated ,
	@CreatedBy ,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	@StateId ,
	@Block ,
	@Street
		   )


	
INSERT INTO [etrade].[OrganizationRequestContacts]
           (
		   [OrganizationRequestId]
           ,[BusinessTelNumber]
           ,[HomeTelNumber]
           ,[BusinessFaxNumber]
           ,[MobileTelNumber]
           ,[EMail]
           ,[WebPageAddress]
           ,[DateCreated]
           ,[CreatedBy]
           --,[DateModified]
           --,[ModifiedBy]
           --,[OwnerLocId]
           --,[OwnerOrgId]
           ,[StateId]
           --,[IsEmailVarified]
           --,[IsMobileVarified]
		   )
     VALUES
           (
		  --@OrganizationRequestContactId int IDENTITY(1,1) NOT =NULL,
	(select id from @InsertedRow) ,--@OrganizationRequestId ,
	@BusinessTelNumber ,
	@HomeTelNumber ,
	@BusinessFaxNumber ,
	@MobileTelNumber ,
	@EMail ,
	@WebPageAddress ,
	GETDATE(),--@DateCreated ,
	@CreatedBy ,
	--@DateModified datetime =NULL,
	--@ModifiedBy varchar(35) =NULL,
	--@OwnerLocId int =NULL,
	--@OwnerOrgId int =NULL,
	@StateId 
	--@IsEmailVarified int =NULL,
	--@IsMobileVarified int =NULL,
		   )


	END
	else 
	begin
	
	 -- SELECT --OReq.OrganizationId AS OrganizationId,  
  -- --ISNULL(OReq.OrganizationId, 0) OrganizationId  
  -- --,'2' AS RequestType  
  -- --,-- edit existing Org  
  -- ISNULL(OReq.NAME, '') OrgEngName  
  -- ,ISNULL(OReq.Description, '') Description  
  -- ,ISNULL(OReq.TradeLicenseNumber, '') TradeLicNumber  
  -- ,ISNULL(OReq.CivilId, '') CivilId  
  -- ,ISNULL(OReq.AuthPerson, '') AuthPerson  
  -- ,ISNULL(OReq.LocalDescription, '') OrgAraName  
  -- ,ISNULL(ORA.PostBoxNumber, '') POBoxNo  ,
  -- --,ISNULL(ORA.Address, '') Address

  --  SUBSTRING(ORA.Address,CHARINDEX(';',ORA.Address,0)+1,LEN(ORA.Address)) as Address
  ---- SUBSTRING(ORA.Address,0,CHARINDEX(';',ORA.Address,0)) as Address

  -- ----,(SELECT TOP 2 VALUE FROM STRING_SPLIT(ISNULL(ORA.Address, ''),'#')) Address   --added siraj   new field      
  -- ,ISNULL(ORA.City, '') City  
  -- ,ISNULL(ORA.STATE, '') STATE  
  -- ,ISNULL(ORA.PostalCode, '') PostalCode  
  -- ,ISNULL(ORA.Country, '') CountryId  
  -- ,ISNULL(ORA.Block, '') Block    --added siraj  new field  
  -- --,ISNULL(ORA.Street, '') Street    --added siraj  new field  
  --, SUBSTRING(ORA.Address,0,CHARINDEX(';',ORA.Address,0)) as Street

  -- ----,REPLACE( (SELECT TOP 1 VALUE FROM STRING_SPLIT(ISNULL(ORA.Address, ''),'#')) ,'Block','') Block    --added siraj  new field  
  -- ,ISNULL(ORC.BusinessTelNumber, '') BusiNo  
  -- ,ISNULL(ORC.BusinessFaxNumber, '') BusiFaxNo  
  -- ,ISNULL(ORC.MobileTelNumber, '') MobileNo  
  -- ,ISNULL(ORC.HomeTelNumber, '') ResidenceNo  
  -- ,ISNULL(ORC.EMail, '') EmailId  
  -- ,ISNULL(ORC.WebPageAddress, '') WebPageAddress  
  -- --,ISNULL((SELECT 0 FROM SubOrg WHERE ChildOrgId=@OrganizationId),1) AS 'IsmainCompany'--Siraj to check if its main company or sub company  
  -- --,CASE   
  -- -- WHEN isnull(OReq.IsIndustrial, '0') = '0'  
  -- --  THEN 'false'  
  -- -- ELSE 'true'  
  -- -- END AS "isIndustrial"  
  -- --,'1' AS Editable  
  -- --,'OrgGetBasicResult' AS "TableName"  
  --FROM dbo.Organizations OReq  
  --LEFT JOIN dbo.Contacts ORC ON OReq.OrganizationId = ORC.ParentId  
  -- AND ORC.ParentType = 'O'  
  --LEFT JOIN dbo.Addresses ORA ON OReq.OrganizationId = ORA.ParentId  
  -- AND ORA.ParentType = 'O'  
  --WHERE OReq.OrganizationId = @OrganizationId  


UPDATE [etrade].[OrganizationRequests]
   SET 
   [RequestType] = null,
      --[RequestNumber] = @RequestNumber, 
       --[RequesterOwner] = @RequesterOwner,
       [Name] = isnull( @Name,(SELECT NAME FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)), 
       [OrganizationId] = @OrganizationId,
       [Description] =(SELECT Description FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId) ,
       [TradeLicenseNumber] = (SELECT TradeLicenseNumber FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       [LocalDescription] = isnull( @LocalDescription,(SELECT NAME FROM ORGANIZATIONS_ARA WHERE ORGANIZATIONID=@OrganizationId)),
       [CivilId] =isnull( @CivilId,(SELECT CivilId FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)) ,
       [AuthPerson] = isnull( @AuthPerson,(SELECT AuthPerson FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)), 
       [CommercialLicenseNo] =  isnull( @CommercialLicenseNo,(SELECT CommercialLicenseNo FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       --[CommercialLicenseValidated] = @CommercialLicenseValidated,
       [CommercialLicenseType] =  isnull( @CommercialLicenseType,(SELECT CommercialLicenseType FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [CommercialLicenseIssueDate] =   isnull( @CommercialLicenseIssueDate,(SELECT CommercialLicenseIssueDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [CommercialLicenseExpiryDate] =   isnull( @CommercialLicenseExpiryDate,(SELECT CommercialLicenseExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [CommercialLicenseSubType] =(SELECT CommercialLicenseSubType FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),-- @CommercialLicenseSubType isnull( @CommercialLicenseExpiryDate,(SELECT CommercialLicenseExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       --[ImporterLicValidated] = @ImporterLicValidated isnull( @CommercialLicenseExpiryDate,(SELECT CommercialLicenseExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [ImporterLicenseNo] =  isnull( @ImporterLicenseNo,(SELECT ImporterLicenseNo FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [ImporterLicenseType] =  isnull( @ImporterLicenseType,(SELECT ImporterLicenseType FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [ImporterLicenseIssueDate] =  isnull( @ImporterLicenseIssueDate,(SELECT ImporterLicenseIssueDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [ImporterLicenseExpiryDate] =  isnull( @ImporterLicenseExpiryDate,(SELECT ImporterLicenseExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       --[IndustrialLicValidated] = @IndustrialLicValidated isnull( @LocalDescription,(SELECT NAME FROM ORGANIZATIONS_ARA WHERE ORGANIZATIONID=@OrganizationId)),
       [IsIndustrial] =  (SELECT IsIndustrial FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       [IndustrialLicenseNo] =  isnull( @IndustrialLicenseNo,(SELECT IndustrialLicenseNo FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),
       [IndIssueDate] =isnull( @IndIssueDate,(SELECT IndIssueDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),-- CASE WHEN  @IndustrialLicenseNo is null then NULL ELSE  isnull( @IndIssueDate,(SELECT IndIssueDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)) END,
       [IndExpiryDate] = isnull( @IndExpiryDate,(SELECT IndExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)),-- CASE WHEN  @IndustrialLicenseNo is null then NULL ELSE  isnull( @IndExpiryDate,(SELECT IndExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)) END,
       [IndRegNo] = (SELECT IndRegNo FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       [IndRegIssuanceDate] = (SELECT IndRegIssuanceDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       --[DateCreated] = @DateCreated,
       --[CreatedBy] = @CreatedBy,
       [DateModified] = getdate(),
       --[ModifiedBy] = @ModifiedBy,
       [OwnerLocId] =  (SELECT OwnerLocId FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       [OwnerOrgId] =  (SELECT OwnerOrgId FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId),
       [StateId] =@StateId,--'OrganizationRequestSubmiState',-- @StateId,
       --[OrgReqDocType] = @OrgReqDocType,
       [RequestedDate] = getdate(),--,--@RequestedDate,
       --[RejectionRemarks] = @RejectionRemarks,
       --[AdditionalInfoRemarks] = @AdditionalInfoRemarks,
       --[RequestForVisitRemarks] = @RequestForVisitRemarks,
       --[IsMainCompany] = @IsMainCompany,
       --[ParentMainCompany] = @ParentMainCompany,
       --[EserviceRequestNumber] = @EserviceRequestNumber
       [ParentOrgTradeLicence] = (SELECT TradeLicenseNumber FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId)
       --[IsUpdated] = @IsUpdated,
       --[ProcessedBy] = @ProcessedBy,
       --[ProcessedDate] = @ProcessedDate,
	   ,AuthPersonNationality=@AuthPersonNationality --isnull( @PostBoxNumber,(SELECT PostBoxNumber FROM Addresses WHERE parentid=@OrganizationId)),
	   ,AuthPersonIssueDate=@AuthPersonIssueDate,
	   [AuthPersonExpiryDate]=@AuthPersonExpiryDate,
	   AuthorizedSignatoryCivilIdExpiryDate=isnull(@AuthorizedSignatoryCivilIdExpiryDate,
	   (SELECT AuthorizedSignatoryCivilIdExpiryDate FROM ORGANIZATIONS WHERE ORGANIZATIONID=@OrganizationId))
	   ,AssignmentStatus='UA'
		,AssignedTo=NULL
		,AssignedDateTime=Null
		,AuthorizerIssuer =@AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025
 WHERE OrganizationRequestId=@OrganizationRequestId --<Search Conditions,,>


UPDATE [etrade].[OrganizationRequestAddresses]
   SET 
   --[OrganizationRequestId] = @OrganizationRequestId,
       [PostBoxNumber] =  isnull( @PostBoxNumber,(SELECT PostBoxNumber FROM Addresses WHERE parentid=@OrganizationId)),
       [City] =  isnull( @City,(SELECT City FROM Addresses WHERE parentid=@OrganizationId)),
       [State] =   isnull( @State,(SELECT State FROM Addresses WHERE parentid=@OrganizationId)),
       [PostalCode] =   isnull( @PostalCode,(SELECT PostalCode FROM Addresses WHERE parentid=@OrganizationId)),
       [Country] =   isnull( @Country,(SELECT Country FROM Addresses WHERE parentid=@OrganizationId)),
       [Address] =   isnull( @Address,(SELECT Address FROM Addresses WHERE parentid=@OrganizationId)),
       [DateCreated] = @DateCreated,
       [CreatedBy] = @CreatedBy,
       --[DateModified] = @DateModified,
       --[ModifiedBy] = @ModifiedBy,
       [OwnerLocId] =  (SELECT [OwnerLocId] FROM Addresses WHERE parentid=@OrganizationId),-- @OwnerLocId,
       [OwnerOrgId] =(SELECT [OwnerOrgId] FROM Addresses WHERE parentid=@OrganizationId),--  @OwnerOrgId,
       [StateId] = @StateId,
       [Block] = isnull( @Block,(SELECT Block FROM Addresses WHERE parentid=@OrganizationId)) ,
       [Street] = isnull( @Street,(SELECT Street FROM Addresses WHERE parentid=@OrganizationId))  ,
       [Floor] = @Floor,--isnull( @Street,(SELECT Street FROM Addresses WHERE parentid=@OrganizationId))  ,
       [ApartmentType] =@ApartmentType,-- isnull( @Street,(SELECT Street FROM Addresses WHERE parentid=@OrganizationId))  ,
       [ApartmentNumber] =@ApartmentNumber-- isnull( @Street,(SELECT Street FROM Addresses WHERE parentid=@OrganizationId)) 
 WHERE [OrganizationRequestId] = @OrganizationRequestId--@Search Conditions,,>
 --SELECT * FROM [etrade].[OrganizationRequestAddresses] ORDER BY 1 desc
  --   SELECT * FROM Addresses WHERE ownerorgid=732857
	 --SELECT * FROM etrade.organizationrequests WHERE  organizationrequestid=59
	 ----SELECT * FROM etrade.mobileuserorgmaps WHERE userid=38
	 --SELECT TOP 10 * FROM contacts
 
UPDATE [etrade].[OrganizationRequestContacts]
   SET 
   --[OrganizationRequestId] = @OrganizationRequestId,
       [BusinessTelNumber] =  isnull( @BusinessTelNumber,(SELECT [BusinessTelNumber] FROM contacts WHERE parentid=@OrganizationId)),
       [HomeTelNumber] =  isnull( @HomeTelNumber,(SELECT [HomeTelNumber] FROM contacts WHERE parentid=@OrganizationId)),
       [BusinessFaxNumber] =  isnull( @BusinessFaxNumber,(SELECT [BusinessFaxNumber] FROM contacts WHERE parentid=@OrganizationId)),
       [MobileTelNumber] =  isnull( @MobileTelNumber,(SELECT [MobileTelNumber] FROM contacts WHERE parentid=@OrganizationId)),
       [EMail] =  isnull( @EMail,(SELECT [EMail] FROM contacts WHERE parentid=@OrganizationId)),
       [WebPageAddress] =  isnull( @WebPageAddress,(SELECT [WebPageAddress] FROM contacts WHERE parentid=@OrganizationId)),
       [DateCreated] = @DateCreated,
       [CreatedBy] = @CreatedBy,
       --[DateModified] = @DateModified,
       --[ModifiedBy] = @ModifiedBy,
       [OwnerLocId] =  (SELECT [OwnerLocId] FROM contacts WHERE parentid=@OrganizationId),-- @OwnerLocId,
       [OwnerOrgId] =(SELECT [OwnerOrgId] FROM contacts WHERE parentid=@OrganizationId),--  @OwnerOrgId,
       [StateId] = @StateId
       --[IsEmailVarified] = @IsEmailVarified,
       --[IsMobileVarified] = @IsMobileVarified,
 WHERE [OrganizationRequestId] = @OrganizationRequestId--@Search Conditions,,>


    
  INSERT INTO etrade.[$OrganizationRequests] (    
   OrganizationRequestId    
   ,RequestType    
   ,RequestNumber    
   ,RequesterOwner    
   ,NAME    
   ,OrganizationId    
   ,Description    
   ,TradeLicenseNumber    
   ,LocalDescription    
   ,CivilId    
   ,AuthPerson    
   ,CommercialLicenseNo    
   ,CommercialLicenseValidated    
   ,CommercialLicenseType    
   ,CommercialLicenseIssueDate    
   ,CommercialLicenseExpiryDate    
   ,CommercialLicenseSubType    
   ,ImporterLicValidated    
   ,ImporterLicenseNo    
   ,ImporterLicenseType    
   ,ImporterLicenseIssueDate    
   ,ImporterLicenseExpiryDate    
   ,IndustrialLicValidated    
   ,IsIndustrial    
   ,IndustrialLicenseNo    
   ,IndIssueDate    
   ,IndExpiryDate    
   ,IndRegNo    
   ,IndRegIssuanceDate    
   ,DateCreated    
   ,CreatedBy    
   ,DateModified    
   ,ModifiedBy    
   ,OwnerLocId    
   ,OwnerOrgId    
   ,StateId    
   ,[$SessionId]    
   ,[$IpId]    
   ,[$ActionDescription]    
   ,[$UserId]    
   ,[$Operation]    
   ,[$DataProfileClassId]    
   ,OrgReqDocType    
   ,RequestedDate    
   ,RejectionRemarks    
   ,[$DateTime]  
   ,
	AuthPersonNationality ,
	AuthPersonIssueDate ,
	AuthPersonExpiryDate  ,AssignmentStatus
,AssignedTo
,AssignedDateTime
,AuthorizerIssuer
   )    
  SELECT OrganizationRequestId    
   ,RequestType    
   ,RequestNumber    
   ,RequesterOwner    
   ,NAME    
   ,OrganizationId    
   ,Description    
   ,TradeLicenseNumber    
   ,LocalDescription    
   ,CivilId    
   ,AuthPerson    
   ,CommercialLicenseNo    
   ,CommercialLicenseValidated    
   ,CommercialLicenseType    
   ,CommercialLicenseIssueDate    
   ,CommercialLicenseExpiryDate    
   ,CommercialLicenseSubType    
   ,ImporterLicValidated    
   ,ImporterLicenseNo    
   ,ImporterLicenseType    
   ,ImporterLicenseIssueDate    
   ,ImporterLicenseExpiryDate    
   ,IndustrialLicValidated    
   ,IsIndustrial    
   ,IndustrialLicenseNo    
   ,IndIssueDate    
   ,IndExpiryDate    
   ,IndRegNo    
   ,IndRegIssuanceDate    
   ,DateCreated    
   ,CreatedBy    
   ,DateModified    
   ,ModifiedBy    
   ,OwnerLocId    
   ,OwnerOrgId    
   ,StateId    
   ,NULL    
   ,NULL    
   ,NULL    
   ,CreatedBy    
   ,'0'    
   ,'OrganizationRequests'    
   ,OrgReqDocType    
   ,RequestedDate    
   ,RejectionRemarks    
   ,GETDATE() 
   ,
	@AuthPersonNationality ,
	@AuthPersonIssueDate  ,
	@AuthPersonExpiryDate
	 ,'UA',NULL,NULL
	 ,AuthorizerIssuer -- rama for authorizer issuer change 16-01-2025
	FROM etrade.OrganizationRequests    
  WHERE OrganizationRequestId = @OrganizationRequestId    
    
   INSERT INTO etrade.[$organizationrequestcontacts]   
                        (organizationrequestid,   
                         organizationrequestcontactid,   
                         businesstelnumber,   
                         hometelnumber,   
                         businessfaxnumber,   
                         mobiletelnumber,   
                         email,   
                         webpageaddress,   
                         [$sessionid],   
                         [$ipid],   
                         [$actiondescription]   
                         /*,[$UserId]   
                         ,[$Operation]   
                         ,[$DataProfileClassId]   
                         ,[$DateTime]*/ -- fields not available Aug13th   
                         ,   
                         createdby,   
                         datecreated,   
                         datemodified,   
                         modifiedby,   
                         ownerlocid,   
                         ownerorgid,   
                         stateid)   
            SELECT OReqC.organizationrequestid,   
                   OReqC.organizationrequestcontactid,   
                   OReqC.businesstelnumber,   
                   OReqC.hometelnumber,   
                   OReqC.businessfaxnumber,   
                   OReqC.mobiletelnumber,   
                   OReqC.email,   
                   OReqC.webpageaddress,   
                   NULL,   
                   NULL,   
                   NULL   
                   /*,@mUserId   
                   ,'1'   
                   ,'OrganizationRequestContacts'   
                   ,GETDATE()*/ -- fields not available Aug13th   
                   ,   
                   OReqC.createdby,   
                   OreqC.datecreated,   
                   OReqC.datemodified,   
                   OReqC.modifiedby,   
                   OReqC.ownerlocid,   
                   OReqC.ownerorgid,   
                   OReqC.stateid   
            FROM   etrade.organizationrequestcontacts OReqC   
            WHERE  OReqC.[organizationrequestid] = @OrganizationRequestId   
  

    INSERT INTO etrade.[$organizationrequestaddresses]   
                        (organizationrequestid,   
                         organizationrequestaddresseid,   
                         postboxnumber,   
                         city,   
                         state,   
                         postalcode,   
                       country,   
                         address,   
                         [$sessionid],   
                         [$ipid],   
                         [$actiondescription],   
                         datecreated,   
                         createdby,   
                         datemodified,   
                         modifiedby,   
                         ownerlocid,   
                         ownerorgid,   
                         stateid ,
[Floor]               ,  
ApartmentType                ,  
ApartmentNumber             )   
            /*,[$DateTime]   
              ,[$UserId]   
              ,[$Operation]   
              ,[$DataProfileClassId]*/---- fields not available Aug13th   
            SELECT OReqA.organizationrequestid,   
                   OReqA.organizationrequestaddresseid,   
                   OReqA.postboxnumber,   
                   OReqA.city,   
                   OReqA.state,   
                   OReqA.postalcode,   
                   OReqA.country,   
                   OReqA.address,   
                   NULL,   
                   NULL,   
                   NULL,   
                   OReqA.datecreated,   
                   OReqA.createdby,   
                   OREqA.datemodified,   
                   OReqA.modifiedby,   
                   OReqA.ownerlocid,   
                   OReqA.ownerorgid,   
                   OReqA.stateid   ,
				   
@Floor                 ,  
@ApartmentType                  ,  
@ApartmentNumber                 
            /*,GETDATE()   
              ,@mUserId   
              ,'1'   
              ,'OrganizationRequestAddresses'*/   
            ---- fields not available Aug13th   
            FROM   etrade.organizationrequestaddresses OReqA   
            WHERE  organizationrequestid = @OrganizationRequestId   
  
DECLARE @ORGREQTYPESUFFIX NVARCHAR(200)='',@NotificationType int=0
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=41),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='NameChange'
SET @NotificationType=8880100
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=42),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='NewImportLicense'
SET @NotificationType=8880200
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=43),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='RenewImportLicense'
SET @NotificationType=8880300
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=44),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='RenewIndustrialLicense'
SET @NotificationType=8880400
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=45),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='RenewCommercialLicense'
SET @NotificationType=8880500
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=46),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='NewAuthorizedSignatory'
SET @NotificationType=8880600
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=47),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='RenewAuthorizedSignatory'
SET @NotificationType=8880700
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=48),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='RemoveAuthorizedSignatory'
SET @NotificationType=8880800
end
if CHARINDEX((SELECT PREFIX  FROM ETRADE.ESERVICES WHERE SERVICEID=49),@RequestNumber) > 0 
begin
SET @ORGREQTYPESUFFIX='ChangeCommercialAddress'
SET @NotificationType=8880900
END



 INSERT INTO [etrade].MobileNotification (    
   [NotificationType]    
   ,[ReferenceId]    
   ,[UserId]    
   ,[DateCreated]    
   ,ReadStatus    
   ,ReffType    
   ,ReffId    
   )    
  SELECT @NotificationType    
   ,@EServiceRequestId--OReq.OrganizationRequestId    
   ,OReq.RequesterOwner    
   ,GETDATE()    
   ,'0'    
   ,'OrganizationRequestSubmit'+'-'+@ORGREQTYPESUFFIX    
   ,OReq.OrganizationRequestId    
  FROM [etrade].OrganizationRequests OReq    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId 


	END



	END
