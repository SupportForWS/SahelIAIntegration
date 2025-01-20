USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[usp_MApp_InsertUpdateOrgRequest]    Script Date: 1/16/2025 4:02:21 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER         procedure [etrade].[usp_MApp_InsertUpdateOrgRequest] (   
@OrganizationRequestId INT = NULL,   
@OrganizationId        INT = NULL,   
@OrgEngName            NVARCHAR(1000) = NULL,   
@OrgAraName            NVARCHAR(1000) = NULL,   
@Description           NVARCHAR(max) = NULL,   
@TradeLicNumber        NVARCHAR(50) = NULL,   
@CivilId               VARCHAR(50) = NULL,   
@AuthPerson            NVARCHAR(50) = NULL,   


        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration 
@AuthPersonNationality            NVARCHAR(50) = NULL,   
@AuthPersonIssueDate            NVARCHAR(50) = NULL,   
@AuthPersonExpiryDate            NVARCHAR(50) = NULL,  
@AuthorizedSignatoryCivilIdExpiryDate            NVARCHAR(50) = NULL,  
        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration  


@POBoxNo               NVARCHAR(100) = NULL,   
-- organizationrequest contact                 
@Address               NVARCHAR(max) = NULL,   
@City                  NVARCHAR(50) = NULL,   
@State                 NVARCHAR(50) = NULL,   
@PostalCode            NVARCHAR(50) = NULL,   
@CountryId             VARCHAR(10) = NULL,   
@BusiNo                NVARCHAR(50) = NULL,   
@BusiFaxNo             NVARCHAR(50) = NULL,   
@MobileNo              VARCHAR(50) = NULL,   
@ResidenceNo           VARCHAR(50) = NULL,   
@EmailId               VARCHAR(50) = NULL,   
@WebPageAddress        VARCHAR(500) = NULL,   
@mUserId               VARCHAR(100) = NULL,   
@Operation             VARCHAR(10) = NULL,   
@IsIndustrial          CHAR(1) = NULL,   
@CompanyType           CHAR(1) = NULL  , 
@Block                  NVARCHAR(200) = NULL,    
@Street                  NVARCHAR(200) = NULL  ,
@ImporterLicenseNo         NVARCHAR(200) = NULL,   
@ImporterLicenseType        int = NULL,   
@ImporterLicenseIssueDate               Datetime = NULL,   
@ImporterLicenseExpiryDate            Datetime = NULL,
@PACIAddress			NVARCHAR(200) = NULL,--islam
 @AuthorizerIssuerId nvarchar(50) =null -- rama for authorizer issuer change 16-01-2025
 

) 
AS   
  BEGIN   

  SET @AuthPersonIssueDate=convert(DATETIME, @AuthPersonIssueDate, 103)-- (CASE WHEN @AuthPersonIssueDate IS NOT NULL THEN REPLACE(@AuthPersonIssueDate,'/','-') ELSE NULL END )
  SET @AuthPersonExpiryDate=convert(DATETIME, @AuthPersonExpiryDate, 103)--(CASE WHEN @AuthPersonExpiryDate IS NOT NULL THEN REPLACE(@AuthPersonExpiryDate,'/','-') ELSE NULL END )
   SET @AuthorizedSignatoryCivilIdExpiryDate=convert(DATETIME, @AuthorizedSignatoryCivilIdExpiryDate, 103)--(CASE WHEN @AuthPersonExpiryDate IS NOT NULL THEN REPLACE(@AuthPersonExpiryDate,'/','-') ELSE NULL END )

      DECLARE @RequestType INT   
      DECLARE @OrganizationIdFromInputParam INT   
      DECLARE @StateId VARCHAR(50)   
      DECLARE @ParentOrgTradeLicence NVARCHAR(150)   
  
      SET @OrganizationIdFromInputParam = Isnull(@OrganizationId, 0)   
  
      --added Siraj   
      -- DECLARE @isAdmin bit,@isSubAdmin bit,@ParentID int   
      --  select @ParentID=ParentID,@isAdmin=isAdmin,@isSubAdmin=isSubAdmin from etrade.mobileuser where userid=@mUserId  
      --  --select @ParentID=ParentID from etrade.mobileuser where userid=@mUserId   
      --  IF(@ParentID is not null)   
      --  BEGIN   
      --  --select @ParentID=ParentID,@isAdmin=isAdmin,@isSubAdmin=isSubAdmin from etrade.mobileuser where userid=(SELECT ParentID FROM etrade.mobileuser where userid=@mUserId)--@mUserId   
      --   SET @mUserId=@ParentID;   
      --  --select @OrganizationIDS=orgid from etrade.mobileuser where userid=@Userid   
      --  END   
      --  ELSE   
      --  BEGIN   
      --     SET @mUserId=@mUserId;   
      --  --select @OrganizationIDS=orgid from etrade.mobileuser where userid=(SELECT ParentID FROM etrade.mobileuser where userid=@Userid)   
      --  END   
      --IF EXISTS(SELECT 1 FROM etrade.MobileUserOrgMaps WHERE UserId=@mUserId)   
      --begin   
      --set @CompanyType=0   
      --SET @ParentOrgTradeLicence=(SELECT TOP 1 ParentOrgTradeLicence FROM etrade.MobileUserOrgMaps WHERE UserId=@mUserId ORDER BY CREATEDDATE DESC )--Added Siraj   
      --end   
      --else   
      --begin   
      --set @CompanyType=1   
      --UPDATE ETRADE.MobileUser SET LicenseNumber=@TradeLicNumber WHERE UserId=@mUserId -- OVERIDDING THE TRADE LICENCE ENTERED DURING REGISTRATION WITH MAIN COMPANY LICENSE   
      --SET @ParentOrgTradeLicence=@TradeLicNumber   
      --end   
      --=================   
      DECLARE @isAdmin    BIT,   
              @isSubAdmin BIT,   
              @ParentID   INT   
  
      SELECT @ParentID = parentid,   
             @isAdmin = isadmin,   
             @isSubAdmin = issubadmin   
      FROM   etrade.mobileuser   
      WHERE  userid = @mUserId   
  
      DECLARE @FirstAppprovedOrg NVARCHAR(100)=   
      --First approved company/Organization under the registered user   
      (SELECT TOP 1 organizationid   
       FROM   etrade.mobileuserorgmaps   
       WHERE  parentorgtradelicence = (SELECT licensenumber   
                                       FROM   etrade.mobileuser   
                                       WHERE  userid = @mUserId)   
       ORDER  BY createddate ASC)   
  
      IF ( EXISTS (SELECT 1   
                   FROM   etrade.mobileuserorgmaps   
                   WHERE  parentorgtradelicence = (SELECT licensenumber   
                                                   FROM   etrade.mobileuser   
                                                   WHERE  userid = @mUserId)) )   
        BEGIN   
            SET @CompanyType = 0   
            SET @ParentOrgTradeLicence = (SELECT licensenumber   
                                          FROM   etrade.mobileuser   
                                          WHERE  userid = @mUserId)   
            --(SELECT TOP 1 ParentOrgTradeLicence FROM etrade.MobileUserOrgMaps WHERE UserId=@mUserId ORDER BY CREATEDDATE DESC )--Added Siraj   
            IF( @OrganizationId IS NOT NULL   
                AND @OrganizationId != 0 )   
              --Additoonal check to identify if its update request for approved main company   
              BEGIN   
                  IF( @FirstAppprovedOrg = @OrganizationId )   
                    BEGIN   
                        SET @CompanyType = 1   
                    END   
              END   
        END   
      ELSE   
        BEGIN   
            SET @CompanyType = 1   
            --UPDATE ETRADE.MobileUser SET LicenseNumber=@TradeLicNumber WHERE UserId=@mUserId -- OVERIDDING THE TRADE LICENCE ENTERED DURING REGISTRATION WITH MAIN COMPANY LICENSE   
            SET @ParentOrgTradeLicence = @TradeLicNumber   
        END   
  
      --added Siraj   
      PRINT '@CompanyType'   
      PRINT @CompanyType   
      PRINT '@CompanyType'   
  
      --Print '1'   
      --Print @OrganizationId   
      IF ( @OrganizationIdFromInputParam = 0 )   
        BEGIN   
            PRINT '2'   
  
            -- Find Request organization available in MC System                       
            set @OrganizationId  = (SELECT top 1 O.organizationid   
            FROM   dbo.organizations O   
                   LEFT JOIN dbo.organizations_ara O_ara   
                          ON O.organizationid = O_ara.organizationid   
            WHERE  O.stateid IN ( 'OrganizationsModifyState',   
                                  'OrganizationsCreatedState',   
                                  'OrganizationsNotActivatedState'   
                                  -- added by pavan on Aug 17th    
                                  , 'OrganizationsOnHoldState'   
                                  -- added by pavan on Aug 17th   
                                  /*,   
                                  'CancelledByReqState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsTransferredState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsCancelledState' */   
                                 -- added by pavan on Aug 17th   
                                 )  
								 AND O.OrganizationId NOT IN( SELECT ExistingOrganizationId FROM OrganizationLinkTable) 
                   AND Isnull(O.tradelicensenumber, '') =   
                       Isnull(@TradeLicNumber, '-')   
       AND ( Isnull(O.localdescription, N'') = Isnull(@OrgAraName, N'')   
         OR Isnull(O.NAME, N'') = Isnull(@OrgAraName, N'')   
         OR Isnull(O_ara.NAME, N'') = Isnull(@OrgAraName, N''))  
     Order by isnull(TotalTransit, 0)+isnull(TotalImport, 0)+isnull(TotalExport, 0)+isnull(TotalTranshipment, 0) desc)                         
       /*AND ( ( Isnull(O.NAME, '') <> ''   
                           AND ( Isnull(O.NAME, '') = Isnull(@OrgEngName, '-1')   
                                  OR Isnull(O.NAME, '') =   
                                     Isnull(@OrgAraName, '-1'   
                                     ) )   
                         )   
                          OR ( Isnull(O_ara.NAME, '') <> ''   
                               AND ( Isnull(O_ara.NAME, '') =   
                                     Isnull(@OrgAraName, '-1')   
                                     AND Isnull(O_ara.NAME, '') =   
                                         Isnull(@OrgEngName, '-1')   
                                   ) ) ) */  
  
            --O.OrganizationCode='IM00038120' And --Hardcode to allow only this company, this shall remove once allow to all.           
            /*AND   
               
                (   
               
                IsNull(O.LocalDescription, N'') = ISNUll(@OrgAraName, N'')   
               
                OR IsNull(O.NAME, N'') = ISNUll(@OrgAraName, N'')   
               
                )*/   
            PRINT @OrganizationId   
        END   
  
      IF ( Isnull(@OrganizationId, '') = '' )   
        BEGIN   
            --Print '3'   
            Set @OrganizationId = (SELECT top 1 O.organizationid   
            FROM   dbo.organizations O   
                   LEFT JOIN dbo.organizations_ara O_ara   
                          ON O.organizationid = O_ara.organizationid   
            WHERE  O.stateid IN ( 'OrganizationsModifyState',   
                                  'OrganizationsCreatedState',   
                                  'OrganizationsNotActivatedState'   
                                  -- added by pavan on Aug 17th    
                                  , 'OrganizationsOnHoldState'   
                                  -- added by pavan on Aug 17th   
                                  /*,   
                                  'CancelledByReqState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsTransferredState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsCancelledState' */  
                                 -- added by pavan on Aug 17th   
                                 )   
								 AND O.OrganizationId NOT IN( SELECT ExistingOrganizationId FROM OrganizationLinkTable)
                   AND ( Isnull(O.tradelicensenumber, '') =   
                         Isnull(@TradeLicNumber, '-')   
                         AND Isnull(O.tradelicensenumber, '') <> '-' )   
                   AND IsNull(O.CivilId, '') = ISNUll(@CivilId, '-1')                 
                   Order by isnull(TotalTransit, 0)+isnull(TotalImport, 0)+isnull(TotalExport, 0)+isnull(TotalTranshipment, 0) desc)  
       /*AND ( ( Isnull(O.NAME, '') <> ''   
                           AND ( Isnull(O.NAME, '') = Isnull(@OrgEngName, '-1')   
                                  OR Isnull(O.NAME, '') =   
                                     Isnull(@OrgAraName, '-1'   
                                     ) )   
                         )   
                          OR ( Isnull(O_ara.NAME, '') <> ''   
                               AND ( Isnull(O_ara.NAME, '') =   
                                     Isnull(@OrgAraName, '-1')   
                                     AND Isnull(O_ara.NAME, '') =   
                                         Isnull(@OrgEngName, '-1')   
                                   ) ) ) */ 
								   
        END   
  
        IF ( Isnull(@OrganizationId, '') = '' )   
        BEGIN   
            --Print '4'   
            set @OrganizationId =   
    (SELECT  top 1 O.organizationid   
    FROM   dbo.organizations O   
    WHERE  O.stateid IN ( 'OrganizationsModifyState',   
                                  'OrganizationsCreatedState',   
                                  'OrganizationsNotActivatedState'   
                                  -- added by pavan on Aug 17th    
                                  , 'OrganizationsOnHoldState'   
                                  -- added by pavan on Aug 17th   
                                  /*,   
                                  'CancelledByReqState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsTransferredState'   
                                  -- added by pavan on Aug 17th   
                                  , 'OrganizationsCancelledState' */  
                                 -- added by pavan on Aug 17th   
                                 )   
								 AND O.OrganizationId NOT IN( SELECT ExistingOrganizationId FROM OrganizationLinkTable)
                   AND ( Isnull(O.tradelicensenumber, '') =   
                         Isnull(@TradeLicNumber, '-')   
                  AND Isnull(O.tradelicensenumber, '') <> '-' )   
      Order by isnull(TotalTransit, 0)+isnull(TotalImport, 0)+isnull(TotalExport, 0)+isnull(TotalTranshipment, 0) desc)  

	  
  PRINT 'inaki'+convert (varchar,@OrganizationId) 
        END   
  
  
      --IF (ISNULL(@OrganizationId, '') = '')                 
      --BEGIN      
      -- SELECT @OrganizationId = O.OrganizationId                 
      -- FROM dbo.Organizations O                 
      -- LEFT JOIN dbo.Organizations_ara O_ara ON O.OrganizationId = O_ara.OrganizationId                
      -- WHERE  O.Stateid in ('OrganizationsModifyState', 'OrganizationsCreatedState') And  IsNull(O.CivilId, '') = ISNUll(@CivilId, '-1')                
      --  AND (                 
      --   (                 
      --    IsNull(O.NAME, '') <> ''                 
      --    AND (                 
      --     IsNull(O.NAME, '') = ISNUll(@OrgEngName, '-1')                 
      --     OR IsNull(O.NAME, '') = ISNUll(@OrgAraName, '-1')                 
      --     )                 
      -- )                 
      --   OR (                 
      --    IsNull(O_ara.NAME, '') <> ''           
      --    AND (                 
      --     IsNull(O_ara.NAME, '') = ISNUll(@OrgAraName, '-1')                 
      --     AND IsNull(O_ara.NAME, '') = ISNUll(@OrgEngName, '-1')                 
      --     )                
      --    )                 
      --   )                 
      -- IF (ISNULL(@OrganizationId, '') = '')                 
      -- BEGIN                 
      --  SELECT @OrganizationId = O.OrganizationId                 
      --  FROM dbo.Organizations O                 
      --  LEFT JOIN dbo.Organizations_ara O_ara ON O.OrganizationId = O_ara.OrganizationId                
      --  WHERE  O.Stateid in ('OrganizationsModifyState', 'OrganizationsCreatedState') And  (               
      --    IsNull(O.CivilId, '') = ISNUll(@CivilId, '-1')                 
      --    AND (                 
      --     IsNull(O.NAME, '') = ISNUll(@OrgEngName, '-1')                 
      --     OR IsNull(O_ara.NAME, '') = ISNUll(@OrgAraName, '-1')                 
      --     )                 
      --    )                 
      -- END                 
      --END     
      --To handle admin user , who depends on parentorgtradelicense and not on userid  ..  Siraj  
      DECLARE @ProceedToUpdateRequest BIT=0   
  
      IF( @isAdmin = 1   
           OR @isSubAdmin = 1 )   

        BEGIN   
		print 'rama'
            IF EXISTS (SELECT 1   
                       FROM   [etrade].organizationrequests   
                       WHERE  organizationrequestid = @OrganizationRequestId   
                              AND parentorgtradelicence = @ParentOrgTradeLicence   
                      )   
              SET @ProceedToUpdateRequest=1   
        END   
      ELSE   
        BEGIN   
            IF EXISTS (SELECT 1   
                       FROM   [etrade].organizationrequests   
                       WHERE  organizationrequestid = @OrganizationRequestId   
                              AND createdby = @mUserId)   
              SET @ProceedToUpdateRequest=1   
        END   
  
  print 'test--------' + convert(varchar,@OrganizationId)
      --To handle admin user , who depends on parentorgtradelicense and not on userid  ..  Siraj  
      IF ( Isnull(@OrganizationRequestId, 0) <> 0 )   
        BEGIN   
            --IF (   
            --    EXISTS (   
            --      SELECT 1   
            --      FROM [etrade].OrganizationRequests   
            --      WHERE OrganizationRequestId = @OrganizationRequestId AND CreatedBy = @mUserId   
            --      )   
            --    )   
            IF( @ProceedToUpdateRequest = 1 )   
              BEGIN   
                  SELECT @StateId = stateid   
                         --,@RequestType = RequestType   
                         ,   
                         @RequestType = CASE   
                                          WHEN Isnull(@OrganizationId, 0) = 0   
                                        THEN   
                                          0   
                                          WHEN EXISTS (select 1 from etrade.MobileUserOrgMaps where UserId = @mUserId and OrganizationId = Isnull(@OrganizationId, -1) and ISNULL(IsActive,0) = 1)
										  THEN 2 
										  ELSE 1

										  -- Insert                 
                                          --WHEN Isnull(@OrganizationId, 0) <> 0   
                                          --     AND @OrganizationIdFromInputParam = 0   
                                        --THEN 1   
                                          -- Update                         
                                          --ELSE 2   
                                        END,   
                         @CountryId = '5150',   
                         @Operation = 'Update'   
                  FROM   [etrade].organizationrequests   
                  WHERE  organizationrequestid = @OrganizationRequestId   

              END   
            ELSE   
              BEGIN   
                  SELECT @OrganizationRequestId     AS OrganizationRequestId,   
                         Isnull(@OrganizationId, 0) AS OrganizatinId,   
                         ''                         AS RequestNumber,   
                         ''                         AS RequestType,   
                         @CompanyType               AS IsMainCompany   
                         -- dummy value as sub company - returning even if no requests present for the received requestid - siraj  
                         ,   
                         '-1'                       AS [Status],   
                         'OrgReq1Result'            AS "TableName"   
  
                  RETURN;   
              END   
        END   
      ELSE   
        BEGIN   
            SELECT @RequestType = CASE   
                                    WHEN Isnull(@OrganizationId, 0) = 0 THEN 0   
                                    -- Insert                 
                                    WHEN Isnull(@OrganizationId, 0) <> 0   
                                         AND @OrganizationIdFromInputParam = 0   
                                  THEN 1   
                                    -- Update                         
                                    ELSE 2   
                                  END,   
                   @CountryId = '5150'   
  PRINT 'inaki'+convert (varchar,@OrganizationId)
				  PRINT 'inaki'+ convert (varchar,@RequestType)
            --Select @StateId = Case When @RequestType =1 then 'OrganizationRequestForUpdateState' Else  'OrganizationRequestForCreateState' END               
            SELECT @StateId = 'OrganizationRequestCreatedState'   
        END   
  
      --print 'RequestType'   
      --print @RequestType   
      IF ( @Operation = 'Update' )   
        BEGIN   
            DECLARE @IsEmailVerified INT = NULL   
  
            IF EXISTS (SELECT 1   
                       FROM   etrade.[organizationrequestcontacts]   
                       WHERE  email IS NOT NULL   
                              AND email = @EmailId   
                              AND organizationrequestid = @OrganizationRequestId   
                              AND isemailvarified = 1)   
              SET @IsEmailVerified = 1   
            ELSE   
              SET @IsEmailVerified = 0   
  
            UPDATE [etrade].organizationrequests   
            SET    NAME = @OrgEngName,   
                   localdescription = @OrgAraName,   
                   [description] = @OrgAraName,   
                   tradelicensenumber = @TradeLicNumber,   
                   civilid = @CivilId,   
                   --OrganizationId = @OrganizationId,    
                   organizationid = CASE   
                                      WHEN @RequestType = 2 THEN @OrganizationId   
                                      ELSE NULL   
                                    END,   
                   authperson = @AuthPerson,   
                   requesttype = @RequestType,   
                   datemodified = Getdate(),   
                   modifiedby = '',   
                   stateid = @StateId,   
                   isindustrial = @IsIndustrial,   
                   ismaincompany = @CompanyType,    
                   AuthPersonNationality = @AuthPersonNationality,   
                   AuthPersonIssueDate = @AuthPersonIssueDate,   
                   AuthPersonExpiryDate = @AuthPersonExpiryDate,  
				   AuthorizedSignatoryCivilIdExpiryDate=@AuthorizedSignatoryCivilIdExpiryDate,
                   parentorgtradelicence = @ParentOrgTradeLicence ,  
				   	   ImporterLicenseNo=@ImporterLicenseNo,
				   ImporterLicenseType=@ImporterLicenseType,
				   ImporterLicenseIssueDate=@ImporterLicenseIssueDate,
				   ImporterLicenseExpiryDate=@ImporterLicenseExpiryDate,
				   AuthorizerIssuer =  @AuthorizerIssuerId  -- rama for authorizer issuer change 16-01-2025

            WHERE  organizationrequestid = @OrganizationRequestId   
  
            UPDATE OCont   
            SET    BusinessTelNumber = @BusiNo,   
                   BusinessFaxNumber = @BusiFaxNo,   
                   MobileTelNumber = @MobileNo,   
                   HomeTelNumber = @ResidenceNo,   
     EMail = @EmailId,   
                   IsEmailVarified = @IsEmailVerified,   
                   WebPageAddress = @WebPageAddress,   
                   DateModified = Getdate(),   
                   ModifiedBy = '',   
                   StateId = 'OrganizationRequestContactsCreatedState'   
            FROM   [etrade].organizationrequestcontacts OCont   
                   INNER JOIN [etrade].organizationrequests O   
                           ON OCont.organizationrequestid =   
                              O.organizationrequestid   
            WHERE  O.organizationrequestid = @OrganizationRequestId   
  
            --Start - Audit Entry             
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
  
            --End - Audit Entry             
            --Updating Organization Address                        
            UPDATE OAdd   
            SET    PostBoxNumber = @POBoxNo,   
                   City = @City,   
                   STATE = @State,   
                   PostalCode = @PostalCode,   
                   Country = @CountryId,   
                   Address = @Address,   
                   DateModified = Getdate(),   
                   ModifiedBy = '',   
                   StateId = 'OrganizationRequestContactsCreatedState'  ,
                   Block =@Block,Street =@Street ,
				   PACIAddressNo = @PACIAddress --islam
            FROM   [etrade].organizationrequestaddresses OAdd   
                   INNER JOIN [etrade].organizationrequests O   
                           ON OAdd.organizationrequestid =   
                              O.organizationrequestid   
            WHERE  O.organizationrequestid = @OrganizationRequestId   
  
            --Start - Audit Entry             
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
                         stateid,
						 PACIAddressNo)   --islam
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
                   OReqA.stateid,
				   OReqA.PACIAddressNo --islam
            /*,GETDATE()   
              ,@mUserId   
              ,'1'   
              ,'OrganizationRequestAddresses'*/   
            ---- fields not available Aug13th   
            FROM   etrade.organizationrequestaddresses OReqA   
            WHERE  organizationrequestid = @OrganizationRequestId   
  
            --End - Audit Entry                         
            SELECT organizationrequestid,   
                   organizationid,   
                   requesttype,   
                   requestnumber,   
                   '0'             AS [Status],   
                   ismaincompany --for company type   
                   ,   
                   'OrgReq1Result' AS "TableName"   
            FROM   organizationrequests   
            WHERE  organizationrequestid = @OrganizationRequestId   
        END   
      ELSE IF ( @Operation = 'Insert' )   
        BEGIN   
            --DECLARE @CounterValue INT   
            --EXEC dbo.usp_MCCounters @Counter = 'EServiceRequests'   
            --  ,@CounterValue = @CounterValue OUTPUT   
            DECLARE @CounterValueStart                 INT = 0,   
                    @CounterValueEnd                   INT = 0,   
                    @EServiceRequestsCounterValueStart INT = 0,   
                    @EServiceRequestsCounterValueEnd   INT = 0   
  
            BEGIN   
                EXEC dbo.Usp_mcpkcounters   
                  @DataSourceName = 'EServiceOrganizationRequests',   
                  -- varchar(50)     
                  -- @SeedValue = @dcCount     
                  --,-- bigint     
                  @CounterValueStart = @CounterValueStart output,-- bigint     
                  @CounterValueEnd = @CounterValueEnd output   
  
                PRINT ( @CounterValueStart )   
  
                --added below counter specifically for eservice request - siraj , using above counter value for eservicerequest table aslso cause conflict in primary key  
                EXEC dbo.Usp_mcpkcounters   
                  @DataSourceName = 'EServiceRequests_pk',-- varchar(50)     
                  -- @SeedValue = @dcCount     
                  --,-- bigint     
                  @CounterValueStart = @EServiceRequestsCounterValueStart output   
                ,   
                  -- bigint     
                  @CounterValueEnd = @EServiceRequestsCounterValueEnd output   
  
                PRINT ( @CounterValueStart )   
            END   
  
            DECLARE @RequestNumber VARCHAR(50)   
            DECLARE @ServicePrefix NVARCHAR(50)   
  
            SET @ServicePrefix = (SELECT prefix   
                                  FROM   etrade.eservices   
                                  WHERE  serviceid = 3)   
  
DECLARE @RequestNumberGEN VARCHAR(50) = @ServicePrefix + '/'   
              + CONVERT(VARCHAR(100), @CounterValueStart)   
              + '/'   
              + CONVERT(VARCHAR(max), RIGHT(Year(Getdate()), 2)) --added siraj   
            DECLARE @EserviceRequestNumber VARCHAR(50)   
  
            INSERT INTO [etrade].[eservicerequests]   
                        (eservicerequestid,   
                         eservicerequestnumber,   
                         serviceid,   
                         datecreated,   
                         stateid,   
                         createdby,   
                         requesteruserid)   
            SELECT @EServiceRequestsCounterValueStart--@CounterValueStart   
                   ,   
                   @RequestNumberGEN   
                   --('ORGREG/' + convert(VARCHAR(100), @CounterValueStart) + '/' + convert(VARCHAR(max), right(year(GETDATE()),2)))     
                   ,   
                   '3',   
                   Getdate() AS [DateCreated],   
                   'EServiceOrganizationRequestCreatedState',   
                   @mUserId,   
                   @mUserId   
  
            --added siraj RequesterUserId in [EServiceRequests] table   
            SELECT @EserviceRequestNumber = @RequestNumberGEN   
  
            --(   
            --select MAX(EserviceRequestId) from etrade.EServiceRequests) --added siraj   
            --  (   
            --select MAX(eservicerequestnumber) from etrade.EServiceRequests)--commented by siraj   
            --select top 1 EserviceRequestId  from etrade.EServiceRequests order by 1 desc)--modified by siraj   
            --DECLARE @RequestNumber VARCHAR(50)   
            --SELECT @RequestNumber = @CounterValue   
            DECLARE @InsertedRow TABLE   
              (   
                 organizationrequestid INT   
              )   
            DECLARE @parentmainCompany INT;   
  
            --Commented Siraj   
            --set @parentmainCompany=(select isnull(OrganizationId,'') from etrade.OrganizationRequests  where ismaincompany=1   
            --and RequesterOwner=@mUserId   
            --)   
            SET @parentmainCompany = (SELECT TOP 1 organizationid   
                                      FROM   etrade.mobileuserorgmaps   
                                      WHERE  --UserId = @mUserId   
            parentorgtradelicence = @ParentOrgTradeLicence   
                                      ORDER  BY createddate ASC) --Added Siraj   
  
            --SET @ParentOrgTradeLicence=(SELECT TOP 1 ParentOrgTradeLicence FROM etrade.MobileUserOrgMaps WHERE UserId=@mUserId ORDER BY CREATEDDATE DESC )--Added Siraj   
            INSERT [etrade].organizationrequests   
                   ([requesttype],   
                    [requestnumber],   
                    [requesterowner],   
                    [name],   
                    [organizationid],   
                    [description],   
                    [tradelicensenumber],   
                    [localdescription],   
                    [civilid],   
                    [authperson],   
                    [datecreated],   
                    [createdby],   
                    [stateid],   
                    isindustrial,   
                    ismaincompany,   
                    parentmaincompany,   
                    eservicerequestnumber,   
                    parentorgtradelicence,
						 AuthPersonNationality,AuthPersonIssueDate,AuthPersonExpiryDate,AuthorizedSignatoryCivilIdExpiryDate,ImporterLicenseNo,ImporterLicenseType,ImporterLicenseIssueDate,ImporterLicenseExpiryDate,AuthorizerIssuer  -- rama for authorizer issuer change 16-01-2025)  
						 )
            output inserted.organizationrequestid   
            INTO @InsertedRow   
            VALUES ( @RequestType   
                     --  ,@RequestNumber   
                     ,   
                     @EserviceRequestNumber,   
                     @mUserId,   
                     @OrgEngName,   
                     CASE   
                       WHEN @RequestType = 2 THEN @OrganizationId   
                       ELSE NULL   
                     END,   
                     --- case statement from Ramesh SP Change Aug13th            
                     @OrgAraName,   
                     @TradeLicNumber,   
          @OrgAraName,   
                     @CivilId,   
                     @AuthPerson,   
                     Getdate(),   
                     @mUserId,   
                     @StateId,   
                     @IsIndustrial,   
                     @CompanyType,   
                     @parentmainCompany,   
                     @EserviceRequestNumber,   
                     @ParentOrgTradeLicence,
						 @AuthPersonNationality,@AuthPersonIssueDate,@AuthPersonExpiryDate,@AuthorizedSignatoryCivilIdExpiryDate,@ImporterLicenseNo,@ImporterLicenseType,@ImporterLicenseIssueDate,@ImporterLicenseExpiryDate,@AuthorizerIssuerId -- rama for authorizer issuer change 16-01-2025)   
						 )   
  
            ---O   
            SELECT @OrganizationRequestId = organizationrequestid   
            FROM   @InsertedRow   
  
            --select @OrganizationRequestId = SCOPE_IDENTITY()                  
            INSERT INTO [etrade].organizationrequestcontacts   
                        ([organizationrequestid],   
                         [businesstelnumber],   
                         [hometelnumber],   
                         [businessfaxnumber],   
                         [mobiletelnumber],   
                         [email],   
                         [webpageaddress],   
                         [datecreated],   
                         [createdby],   
                         [stateid])   
            VALUES      ( @OrganizationRequestId,   
                          @BusiNo,   
                          @ResidenceNo,   
                          @BusiFaxNo,   
                          @MobileNo,   
                          @EmailId,   
                          @WebPageAddress,   
                          Getdate(),   
                          @mUserId,   
                          'OrganizationRequestContactsCreatedState' )   
  
            INSERT INTO [etrade].organizationrequestaddresses   
                        ([organizationrequestid],   
                         [postboxnumber],   
                         [city],   
                         [state],   
                         [postalcode],   
                         [country],   
                         [address],   
                         [datecreated],   
                         [createdby],   
                         [stateid],
                         Block,
                         Street,
						 PACIAddressNo)   --islam
            VALUES      ( @OrganizationRequestId,   
                          @POBoxNo,   
                       --   @City,   
                       --   @State,   
					     (select top 1  regionara from etrade.KuwaitPostalCodes where RegionId in (select Region from etrade.MobileUser where UserId=@mUserId)),
						
                         (select top 1 GovernorateAra from etrade.KuwaitPostalCodes where GovernorateId in (select Governorate from etrade.MobileUser where UserId=@mUserId)),
                         @PostalCode,   
                          @CountryId,   
                          @Address,   
                          Getdate(),   
                          @mUserId,   
                          'OrganizationRequestAddressesCreatedState' 
                          ,@Block
                          ,@Street,
						  @PACIAddress)   --islam
  
            --commented siraj   
            --SELECT @OrganizationRequestId AS OrganizationRequestId   
            --  ,ISNULL(@OrganizationId, 0) AS OrganizatinId   
            --  ,@RequestNumber AS RequestNumber   
            --  ,@RequestType AS RequestType   
            --  ,'0' AS [Status]   
            --  ,'OrgReq1Result' AS "TableName"   
            --added below siraj   
            SELECT organizationrequestid,   
                   organizationid,   
                   requesttype,   
                   requestnumber,   
                   '0'             AS [Status],   
                   ismaincompany --for company type   
                   ,   
                   'OrgReq1Result' AS "TableName"   
            FROM   organizationrequests   
            WHERE  organizationrequestid = @OrganizationRequestId   
        END   
  END   
  
