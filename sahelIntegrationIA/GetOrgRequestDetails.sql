USE [MicroclearLight_July23]
GO
/****** Object:  StoredProcedure [etrade].[usp_MApp_GetOrgRequestDetails]    Script Date: 1/16/2025 12:28:11 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

--[etrade].[usp_MApp_GetOrgRequestDetails] 224,25

ALTER     PROCEDURE  [etrade].[usp_MApp_GetOrgRequestDetails]    
(    
@OrganizationRequestId int,    
@mUserid int    
)    
AS    
BEGIN    
    
DECLARE @ISADMIN BIT = 0    
  ,@ISSUBADMIN BIT = 0    
  ,@Licensenumber NVARCHAR(150)    
  ,@ParentOrgTradeLicence NVARCHAR(150)    
      
SELECT @ISADMIN = isnull(ISADMIN,0)    
  ,@ISSUBADMIN = isnull(ISSUBADMIN,0)    
  ,@Licensenumber = LicenseNumber    
  ,@ParentOrgTradeLicence=LicenseNumber    
 FROM ETRADE.MobileUser    
 WHERE UserId = @mUserId    
    
	--Select * from etrade.OrganizationRequests where  OrganizationRequestId =173     
 ----and CreatedBy =@mUserid )--commented Siraj     
 --and (CreatedBy =13  ) and ParentOrgTradeLicence=@ParentOrgTradeLicence 

 If(Exists(Select * from etrade.OrganizationRequests where  OrganizationRequestId =@OrganizationRequestId     
 --and CreatedBy =@mUserid )--commented Siraj     
 and (CreatedBy =@mUserid or @ISADMIN=1 or @ISSUBADMIN=1) and ParentOrgTradeLicence=@ParentOrgTradeLicence ))--added Siraj -- modified to get details of requests which was not created by logged in admin user    
 Begin    
     
  Select 'FromORGReq' as "Source", 'OrgReqDataFrom' as "TableName"       
  SELECT    
    OReq.OrganizationRequestId AS OrganizationRequestId,    
    OReq.RequestNumber AS RequestNumber,--added newly    
    OReq.RequestType as RequestType,    
   ISNULL(OReq.OrganizationId, 0) OrganizationId,    
   ISNULL(OReq.NAME, '') OrgEngName,    
   ISNULL(OReq.Description, '') Description,    
   ISNULL(OReq.TradeLicenseNumber, '') TradeLicNumber,    
   ISNULL(OReq.CivilId, '') CivilId,    
   ISNULL(OReq.AuthPerson, '') AuthPerson,    

   ISNULL(CONVERT( VARCHAR, OReq.AuthPersonNationality, 103 ) , '') Nationality,--AuthPersonNationality,   
   ISNULL(CONVERT( VARCHAR, OReq.AuthPersonIssueDate, 103 ) , '') IssueDate,--AuthPersonIssueDate,    
   ISNULL(CONVERT( VARCHAR, OReq.AuthPersonExpiryDate, 103 ) , '') ExpiryDate,--AuthPersonExpiryDate,    
   ISNULL(CONVERT( VARCHAR, OReq.AuthorizedSignatoryCivilIdExpiryDate, 103 ) , '') AuthorizedSignatoryCivilIdExpiryDate,--AuthPersonExpiryDate,      

   ISNULL(OReq.LocalDescription, '') OrgAraName,    
   --ISNULL(OReq.IsMainCompany, '') IsmainCompany,--added siraj    
   ISNULL(ORA.PostBoxNumber, '') POBoxNo,    
   ISNULL(ORA.Address, '') Address,    
   ISNULL(ORA.City+'/'+KP.RegionAra, ORA.City) City,    
 -- (select RegionAra from etrade.KuwaitPostalCodes where regioneng=ORA.City ) As g,
 
   ISNULL(ORA.STATE+'/'+kp.GovernorateAra, ORA.STATE) State,    
   ISNULL(ORA.PostalCode, '') PostalCode,    
   ISNULL(ORA.Block, '') Block,    --added siraj  new field  
   ISNULL(ORA.Street, '') Street,   --added siraj   new field      
   ISNULL(ORA.Country, '') CountryId,    
   ISNULL(ORC.BusinessTelNumber, '') BusiNo,    
   ISNULL(ORC.BusinessFaxNumber, '') BusiFaxNo,    
   ISNULL(ORC.MobileTelNumber, '') MobileNo,    
   ISNULL(ORC.HomeTelNumber, '') ResidenceNo,    
   ISNULL(ORC.EMail, '') EmailId,    
   ISNULL(ORC.WebPageAddress, '') WebPageAddress,    
   Case When isnull(OReq.IsIndustrial,'0')='0' then 'false' else 'true' end as "isIndustrial",    
   Case When isnull(OReq.IsMainCompany,'0')='0' then 'false' else 'true' end as "IsMainCompany",    
   OReq.StateId ,   
   
      ORAWf.StateName+ '/' +ORAWfEN.StateName as stateidAra, 
   Case When     
   OReq.StateId in ('OrganizationRequestForCreateState','OrganizationRequestForUpdateState','OrganizationRequestApprovedForCreate','OrganizationRequestApprovedForUpdate','OrganizationRequestForVisit')    
   Then    
   '0'    
   ELSE     
   '1'    
   END  as Editable,  
      ISNULL(OReq.AuthorizerIssuer, '') AuthorizerIssuerId,   -- rama for authorizer issuer change 16-01-2025
 
   'OrgRequestGetBasicResult' AS "TableName"    
  FROM [etrade].OrganizationRequests OReq    
  LEFT JOIN [etrade].OrganizationRequestContacts ORC ON OReq.OrganizationRequestId = ORC.OrganizationRequestId    
  LEFT JOIN [etrade].OrganizationRequestAddresses ORA ON OReq.OrganizationRequestId = ORA.OrganizationRequestId 
  LEFT JOIN DataProfileClassWFStates_ara ORAWf ON ORAWf.StateId = OReq.StateId
  LEFT JOIN DataProfileClassWFStates ORAWfEN ON ORAWfEN.StateId = OReq.StateId
  left join etrade.KuwaitPostalCodes KP on Kp.RegionEng=ORA.City
     
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId    


    

--	select * from DataProfileClassWFStates_ara where StateId='OrganizationRequestForCreateState'
    
  -- Select * from DataProfileClassWFStates Where DataProfileClassId ='OrganizationRequests'    
  -- Industrial Info    
  SELECT OReq.OrganizationRequestId AS OrganizationRequestId,     
   ISNULL(OReq.OrganizationId, 0) OrganizationId,    
   ISNULL(OReq.IsIndustrial, '') IsIndustrial,    
   ISNULL(OReq.IndustrialLicenseNo, '') IndustrialLicenseNumber,    
   CONVERT(VARCHAR(10), OReq.IndIssueDate, 103)  AS IssueDate,--added siraj    
    CONVERT(VARCHAR(10), OReq.IndExpiryDate, 103)  AS ExpiryDate,--added siraj    
   ISNULL(OReq.IndRegNo, '') IndustrialRegistrationNumber,    
   CONVERT(VARCHAR(10), OReq.IndRegIssuanceDate, 103)  IssuanceDate,--added siraj  
    
        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration 
   ISNULL(  OReq.AuthPersonNationality, '')  AS AuthPersonNationality,--added siraj    
    CONVERT(VARCHAR(10), OReq.AuthPersonIssueDate, 103)  AS AuthPersonIssueDate,--added siraj    
    CONVERT(VARCHAR(10), OReq.AuthPersonExpiryDate, 103)  AS AuthPersonExpiryDate,--added siraj   
        --//ADDED 29/08/2021  as per request from abdel salam and ramesh to add more details of authorized person during registration 
	  
   'OrgRequestGetIndustrialResult' AS "TableName"    
  FROM [etrade].OrganizationRequests OReq    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId    
    
  -- Importer Info    
  SELECT OReq.OrganizationRequestId AS OrganizationRequestId,    
   ISNULL(OReq.OrganizationId, 0) OrganizationId,    
   --Case when OReq.ImporterLicenseType =18 then 'permanent' when OReq.ImporterLicenseType =19 then 'temporary' Else '0' end as ImpLicType,   
   Case when OReq.ImporterLicenseType =18 then N'permanent / دائم' when OReq.ImporterLicenseType =19 then N'temporary / مؤقت' Else '0' end as ImpLicType,     
   ISNULL(OReq.ImporterLicenseNo, '') ImpLicNo,    
   CONVERT(VARCHAR(10),  OReq.ImporterLicenseIssueDate, 103)  AS IssueDate,    
   CONVERT(VARCHAR(10),  OReq.ImporterLicenseExpiryDate, 103)  AS ExpiryDate,    
   'OrgRequestGetImportLicenseResult' AS "TableName"    
  FROM [etrade].OrganizationRequests OReq    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId    
    
  -- Comercial Info    
  SELECT OReq.OrganizationRequestId,    
   ISNULL(OReq.OrganizationId, 0) OrganizationId,    
   --Case when OReq.CommercialLicenseType =3 then 'personal' when OReq.CommercialLicenseType =4 then 'industrial' when OReq.CommercialLicenseType =5 then 'corporation'  Else '0' end as  CommLicType,    
   Case when OReq.CommercialLicenseType =3 then N'personal / فردي ' when OReq.CommercialLicenseType =4 then N'industrial / شركات اشخاص' when OReq.CommercialLicenseType =5 then N'corporation / مساهمة'  Else '0' end as  CommLicType,   
   ISNULL(OReq.CommercialLicenseSubType, '') CommLicSubType,    
   ISNULL(OReq.CommercialLicenseNo, '') CommLicNo,    
   CONVERT(VARCHAR(10),  OReq.CommercialLicenseIssueDate, 103)  AS IssueDate,    
   CONVERT(VARCHAR(10),  OReq.CommercialLicenseExpiryDate, 103) AS ExpiryDate,    
   'OrgRequestGetCommercialLicenseResult' AS "TableName"    
  FROM [etrade].OrganizationRequests OReq    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId    
    
  -- AuthorizedSignatories Info    
  SELECT (SELECT Description FROM ETRADE.OrganizationRequests WHERE OrganizationRequestId = @OrganizationRequestId) 'OrgAraName'    
      ,OrgAuthorizedSignatoryId    
   ,ISNULL(OrganizationRequestId, 0) OrganizationRequestId    
   ,ISNULL(OrganizationId, 0) OrganizationId    
   ,AuthorizedPerson 'AuthPerson'    
   ,ISNULL(CivilId ,'') 'CivilId'    
   ,0 'NewPerson'    
   ,'OrgAuthorizedSignatories' as TableName     
  FROM [etrade].OrgRequestAuthorizedSignatories    
  WHERE OrganizationRequestId = @OrganizationRequestId    
    
    
  declare @lang varchar(20);    
    
  set @lang=(select Lang from etrade.MobileUserSession where UserId=@mUserid)    
    
  if(@lang='ar')    
  begin    
  -- Document Info      
  SELECT  OReq.OrganizationRequestId,    
 OReq.scanRequestUploadDocId as OrganizationRequestDocumentId --qasem28-4 
  -- ,OrganizationRequestDocumentId  
  , ISNULL(OReq.DocumentName, 0) AS DocumentName,    
   ISNULL(OReq.DocumentType, '') AS DocumentType,    
  -- ISNULL(T.Code, '') AS DocumentTypeCode,    
   ISNULL(T.Name, '') AS DocumentTypeCode,    
     CONVERT(VARCHAR(10),  Oreq.datecreated, 103) as Createddate,  
       
   'Encryptedid' as Encryptedid,   
   'OrgRequestGetDocumentsResult' AS "TableName"    
  FROM [etrade].OrganizationRequestDocuments OReq    
  Left Join Types_ara T ON T.TypeId =  ISNULL(OReq.DocumentType, '') AND ISNULL(OReq.DocumentType, '')<>''    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId And OReq.StateId='OrgReqDocumentsCreatedState'    
      
 end    
    
    
 else    
    
 begin    
    
 SELECT 
 OReq.OrganizationRequestId,    
 OReq.scanRequestUploadDocId as OrganizationRequestDocumentId --qasem28-4
  -- ,OrganizationRequestDocumentId 
   ,ISNULL(OReq.DocumentName, 0) AS DocumentName,    
   ISNULL(OReq.DocumentType, '') AS DocumentType,    
  -- ISNULL(T.Code, '') AS DocumentTypeCode,    
   ISNULL(T.Name, '') AS DocumentTypeCode,    
         CONVERT(VARCHAR(10),  Oreq.datecreated, 103) as Createddate,  
  
      
   'Encryptedid' as Encryptedid,    
   'OrgRequestGetDocumentsResult' AS "TableName"    
  FROM [etrade].OrganizationRequestDocuments OReq    
  Left Join Types_ara T ON T.TypeId =  ISNULL(OReq.DocumentType, '') AND ISNULL(OReq.DocumentType, '')<>''    
  WHERE OReq.OrganizationRequestId = @OrganizationRequestId And OReq.StateId='OrgReqDocumentsCreatedState'    
     
    
 end    
End    
    
    
end    




-- select * from etrade.KuwaitPostalCodes
