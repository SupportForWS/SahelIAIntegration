USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[usp_MApp_GetNewFromOrganizationsDetails]    Script Date: 1/16/2025 3:49:03 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

 ALTER     PROCEDURE [etrade].[usp_MApp_GetNewFromOrganizationsDetails] (  
 @OrganizationId BIGINT  
 ,@mUserId BIGINT  
 ,@ApprovedDetail bit = 1-- Added to differentiate if its to view approved detail or Update request detail - Siraj  
 -- Earlier , this sp will check if there is one request for update , if so it will return the request detail only but not approved detail  
 )  
AS  
BEGIN  
 DECLARE @OrganizationRequestId AS INT  
  
  -- SELECT *  
  --  FROM etrade.MobileUserOrgMaps M  
  --  WHERE M.OrganizationId = 1535465  
  --   AND M.ParentOrgTradeLicence =(SELECT LICENSENUMBER FROM ETRADE.MobileUser WHERE UserId= 13)  
	 --SELECT * FROM ETRADE.MobileUser WHERE UserId= 13
 --IF (  
 --  NOT EXISTS (  
 --   SELECT 1  
 --   FROM etrade.MobileUserOrgMaps M  
 --   WHERE M.OrganizationId = @OrganizationId  
 --    AND M.UserId = @mUserId  
 --   )  
 --  )  
 -- RETURN;  
--Commented above and added below query- to cover the case - to get associated organization details which was created by different user - Siraj  
IF (  
   NOT EXISTS (  
    SELECT 1  
    FROM etrade.MobileUserOrgMaps M  
    WHERE M.OrganizationId = @OrganizationId  
     AND M.ParentOrgTradeLicence =(SELECT LICENSENUMBER FROM ETRADE.MobileUser WHERE UserId= @mUserId)  
    )  
   )  
  RETURN;  
    
  --siraj  
    
  
 SELECT @OrganizationRequestId = OrganizationRequestId  
 FROM etrade.OrganizationRequests  
 WHERE OrganizationId = @OrganizationId AND REQUESTNUMBER LIKE  'OR/%' --TO CHECK ONLY ORG CREATE OR UPDATE REQUEST ,EXCLUDING OTHER ORG SERVICE REQUESTS
  AND CreatedBy = @mUserid  
  AND StateId = 'OrganizationRequestCreatedState'  
  print '1'  
  print 'orgReq'  
  print @OrganizationRequestId  
     
    IF(@ApprovedDetail=1)--Added to differentiate if its to view approved detail or Update request detail - Siraj  
    BEGIN  
      
      SELECT 'FromORG' AS "Source"  
   ,'OrgReqDataFrom' AS "TableName"  
  
  SELECT --OReq.OrganizationId AS OrganizationId,  
   ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,'2' AS RequestType  
   ,-- edit existing Org  
   --ISNULL(OReq.NAME, '') OrgEngName  
   (case when ISNULL(OReq.NAME, '') ='' THEN Oreq_ARa.Name ELSE  ISNULL(OReq.NAME, '')  END )  OrgEngName
   ,ISNULL(OReq.Description, '') Description  
   ,ISNULL(OReq.TradeLicenseNumber, '') TradeLicNumber  
   ,ISNULL(OReq.CivilId, '') CivilId  
   ,ISNULL(OReq.AuthPerson, '') AuthPerson  
   --SELECT * FROM addresses where parentid=480
   
        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration 
   , null AS AuthPersonNationality,--added siraj    --ISNULL(  OReq.AuthPersonNationality, '')
    null   AS AuthPersonIssueDate,--added siraj    --CONVERT(VARCHAR(10), OReq.AuthPersonIssueDate, 103)
    null  AS AuthPersonExpiryDate,--added siraj   --CONVERT(VARCHAR(10), OReq.AuthPersonExpiryDate, 103)
	AuthorizedSignatoryCivilIdExpiryDate AS AuthorizedSignatoryCivilIdExpiryDate
        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration 

   ,ISNULL(OReq.LocalDescription, '') OrgAraName  
   ,ISNULL(ORA.PostBoxNumber, '') POBoxNo  ,
   --,ISNULL(ORA.Address, '') Address

    SUBSTRING(ORA.Address,CHARINDEX(';',ORA.Address,0)+1,LEN(ORA.Address)) as Address
  -- SUBSTRING(ORA.Address,0,CHARINDEX(';',ORA.Address,0)) as Address

   ----,(SELECT TOP 2 VALUE FROM STRING_SPLIT(ISNULL(ORA.Address, ''),'#')) Address   --added siraj   new field      
   ,ISNULL(ORA.City, '') City  
   ,ISNULL(ORA.STATE, '') STATE  
   ,ISNULL(ORA.PostalCode, '') PostalCode  
   ,ISNULL(ORA.Country, '') CountryId  
   ,ISNULL(ORA.Block, '') Block    --added siraj  new field  
   ,ISNULL(ORA.Street, '') Street    --added siraj  new field 
   ,ISNULL(ORA.PACIAddressNo, '') PACIAddressNo    --added islam  new field 
  --, SUBSTRING(ORA.Address,0,CHARINDEX(';',ORA.Address,0)) as Street

   ----,REPLACE( (SELECT TOP 1 VALUE FROM STRING_SPLIT(ISNULL(ORA.Address, ''),'#')) ,'Block','') Block    --added siraj  new field  
   ,ISNULL(ORC.BusinessTelNumber, '') BusiNo  
   ,ISNULL(ORC.BusinessFaxNumber, '') BusiFaxNo  
   ,ISNULL(ORC.MobileTelNumber, '') MobileNo  
   ,ISNULL(ORC.HomeTelNumber, '') ResidenceNo  
   ,ISNULL(ORC.EMail, '') EmailId  
   ,ISNULL(ORC.WebPageAddress, '') WebPageAddress  
   ,ISNULL((SELECT 0 FROM SubOrg WHERE ChildOrgId=@OrganizationId),1) AS 'IsmainCompany'--Siraj to check if its main company or sub company  
   ,CASE   
    WHEN isnull(OReq.IsIndustrial, '0') = '0'  
     THEN 'false'  
    ELSE 'true'  
    END AS "isIndustrial"  
   ,'1' AS Editable  

   ,'OrgGetBasicResult' AS "TableName"  
  FROM dbo.Organizations OReq  INNER JOIN dbo.Organizations_ara Oreq_ARa ON OReq.OrganizationId = Oreq_ARa.OrganizationId  
  LEFT JOIN dbo.Contacts ORC ON OReq.OrganizationId = ORC.ParentId  
   AND (ORC.ParentType = 'O'  or ORC.ParentType is null)
  LEFT JOIN dbo.Addresses ORA ON OReq.OrganizationId = ORA.ParentId  
  And (ORA.ParentType = 'O'  or ORA.ParentType is null)
  WHERE OReq.OrganizationId = @OrganizationId  
  
  print '3'  
  -- Industrial Info  
  SELECT OReq.OrganizationId AS OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,ISNULL(OReq.IsIndustrial, '') IsIndustrial  
   ,ISNULL(OReq.IndustrialLicenseNo, '') IndustrialLicenseNumber  
   ,CONVERT(VARCHAR(10), OReq.IndIssueDate, 103)  AS IssueDate--Siraj   
   ,CONVERT(VARCHAR(10), OReq.IndExpiryDate, 103)  AS ExpiryDate--Siraj   
   ,ISNULL(OReq.IndRegNo, '') IndustrialRegistrationNumber  
   ,CONVERT(VARCHAR(10), OReq.IndRegIssuanceDate, 103)  IssuanceDate--Siraj   
   ,'OrgGetIndustrialResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Importer Info  
  SELECT OReq.OrganizationId AS OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,CASE   
    WHEN OReq.ImporterLicenseType = 18  
     THEN 'permanent'  
    WHEN OReq.ImporterLicenseType = 19  
     THEN 'temporary'  
    ELSE '0'  
    END AS ImpLicType  
   ,ISNULL(OReq.ImporterLicenseNo, '') ImpLicNo  
   ,CONVERT(VARCHAR(10), OReq.ImporterLicenseIssueDate, 103)   AS IssueDate  
   ,CONVERT(VARCHAR(10), OReq.ImporterLicenseExpiryDate, 103)    AS ExpiryDate  
   ,'OrgGetImportLicenseResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Comercial Info  
  SELECT OReq.OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,CASE   
    WHEN OReq.CommercialLicenseType = 3  
     THEN 'personal'  
    WHEN OReq.CommercialLicenseType = 4  
     THEN 'industrial'  
    WHEN OReq.CommercialLicenseType = 5  
     THEN 'corporation'  
    ELSE '0'  
    END AS CommLicType  
   ,ISNULL(OReq.CommercialLicenseSubType, '') CommLicSubType  
   ,ISNULL(OReq.CommercialLicenseNo, '') CommLicNo  
   ,CONVERT(VARCHAR(10), OReq.CommercialLicenseIssueDate, 103)  AS IssueDate  
   ,CONVERT(VARCHAR(10), OReq.CommercialLicenseExpiryDate, 103)   AS ExpiryDate  
   ,'OrgGetCommercialLicenseResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Document Info    
  SELECT OReq.OrganizationRequestId  
   ,ISNULL(OReq.DocumentName, 0) AS DocumentName  
   ,ISNULL(OReq.DocumentType, '') AS DocumentType  
   ,'OrgGetOrgReqDocumentsResult' AS "TableName"  
  FROM [etrade].OrganizationRequestDocuments OReq  
  WHERE 1 = 2  
      
    RETURN;  
    END  
    
 IF (Isnull(@OrganizationRequestId, 0) = 0  )  
 BEGIN  
  print '2'  
  SELECT 'FromORG' AS "Source"  
   ,'OrgReqDataFrom' AS "TableName"  
  
  SELECT --OReq.OrganizationId AS OrganizationId,  
   ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,'2' AS RequestType  
   ,-- edit existing Org  
   ISNULL(OReq.NAME, '') OrgEngName  
   ,ISNULL(OReq.Description, '') Description  
   ,ISNULL(OReq.TradeLicenseNumber, '') TradeLicNumber  
   ,ISNULL(OReq.CivilId, '') CivilId  
   ,ISNULL(OReq.AuthPerson, '') AuthPerson  
   ,ISNULL(OReq.LocalDescription, '') OrgAraName  
   ,ISNULL(ORA.PostBoxNumber, '') POBoxNo  
   ,ISNULL(ORA.Address, '') Address  
   ,ISNULL(ORA.City, '') City  
   ,ISNULL(ORA.STATE, '') STATE  
   ,ISNULL(ORA.PostalCode, '') PostalCode  
   ,ISNULL(ORA.Country, '') CountryId  
   ,ISNULL(ORA.PACIAddressNo, '') PACIAddressNo    --added islam  new field 
   --,ISNULL(ORA.Block, '') Block,    --added siraj  new field  
   --ISNULL(ORA.Street, '') Street   --added siraj   new field    
   ,ISNULL(ORC.BusinessTelNumber, '') BusiNo  
   ,ISNULL(ORC.BusinessFaxNumber, '') BusiFaxNo  
   ,ISNULL(ORC.MobileTelNumber, '') MobileNo  
   ,ISNULL(ORC.HomeTelNumber, '') ResidenceNo  
   ,ISNULL(ORC.EMail, '') EmailId  
   ,ISNULL(ORC.WebPageAddress, '') WebPageAddress  
   ,ISNULL((SELECT 0 FROM SubOrg WHERE ChildOrgId=@OrganizationId),1) AS 'IsmainCompany'--Siraj to check if its main company or sub company  
   ,CASE   
    WHEN isnull(OReq.IsIndustrial, '0') = '0'  
     THEN 'false'  
    ELSE 'true'  
    END AS "isIndustrial"  
   ,'1' AS Editable  
   ,'OrgGetBasicResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  LEFT JOIN dbo.Contacts ORC ON OReq.OrganizationId = ORC.ParentId  
   AND (ORC.ParentType = 'O'  or ORC.ParentType is null)
  LEFT JOIN dbo.Addresses ORA ON OReq.OrganizationId = ORA.ParentId  
   AND (ORA.ParentType = 'O'  or ORA.ParentType is null)
  WHERE OReq.OrganizationId = @OrganizationId  
  
  print '3'  
  -- Industrial Info  
  SELECT OReq.OrganizationId AS OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,ISNULL(OReq.IsIndustrial, '') IsIndustrial  
   ,ISNULL(OReq.IndustrialLicenseNo, '') IndustrialLicenseNumber  
   ,CONVERT(VARCHAR(10), OReq.IndIssueDate, 103)  AS IssueDate--Siraj   
   ,CONVERT(VARCHAR(10), OReq.IndExpiryDate, 103)  AS ExpiryDate--Siraj   
   ,ISNULL(OReq.IndRegNo, '') IndustrialRegistrationNumber  
   ,CONVERT(VARCHAR(10), OReq.IndRegIssuanceDate, 103)  IssuanceDate--Siraj   
   ,'OrgGetIndustrialResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Importer Info  
  SELECT OReq.OrganizationId AS OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,CASE   
    WHEN OReq.ImporterLicenseType = 18  
     THEN 'permanent'  
    WHEN OReq.ImporterLicenseType = 19  
     THEN 'temporary'  
    ELSE '0'  
    END AS ImpLicType  
   ,ISNULL(OReq.ImporterLicenseNo, '') ImpLicNo  
   ,CONVERT(VARCHAR(10), OReq.ImporterLicenseIssueDate, 103)   AS IssueDate  
   ,CONVERT(VARCHAR(10), OReq.ImporterLicenseExpiryDate, 103)    AS ExpiryDate  
   ,'OrgGetImportLicenseResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Comercial Info  
  SELECT OReq.OrganizationId  
   ,ISNULL(OReq.OrganizationId, 0) OrganizationId  
   ,CASE   
    WHEN OReq.CommercialLicenseType = 3  
     THEN 'personal'  
    WHEN OReq.CommercialLicenseType = 4  
     THEN 'industrial'  
    WHEN OReq.CommercialLicenseType = 5  
     THEN 'corporation'  
    ELSE '0'  
    END AS CommLicType  
   ,ISNULL(OReq.CommercialLicenseSubType, '') CommLicSubType  
   ,ISNULL(OReq.CommercialLicenseNo, '') CommLicNo  
   ,CONVERT(VARCHAR(10), OReq.CommercialLicenseIssueDate, 103)  AS IssueDate  
   ,CONVERT(VARCHAR(10), OReq.CommercialLicenseExpiryDate, 103)   AS ExpiryDate  
   ,'OrgGetCommercialLicenseResult' AS "TableName"  
  FROM dbo.Organizations OReq  
  WHERE OReq.OrganizationId = @OrganizationId  
  
  -- Document Info    
  SELECT OReq.OrganizationRequestId  
   ,ISNULL(OReq.DocumentName, 0) AS DocumentName  
   ,ISNULL(OReq.DocumentType, '') AS DocumentType  
   ,'OrgGetOrgReqDocumentsResult' AS "TableName"  
  FROM [etrade].OrganizationRequestDocuments OReq  
  WHERE 1 = 2  
 END  
 ELSE  
 BEGIN  
  EXEC [etrade].[usp_MApp_GetOrgRequestDetails] @OrganizationRequestId = @OrganizationRequestId  
   ,@mUserid = @mUserid  
 END  
END  
  

  
