﻿using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Runtime.Serialization;
using System.ServiceModel.Description;
using Microsoft.Xrm.Sdk.Client;
using System.Net;

namespace ItAintBoring.EZChange.Core.Dynamics
{
    public class DynamicsService
    {



        public DynamicsService(string connectionString)
        {
            this.connectionString = connectionString;
        }

        private string connectionString = null;
        public string ConnectionString
        {
            get
            {
                return connectionString;
            }
            set
            {
                if (connectionString != value)
                {
                    connectionString = value;
                    internalService = null;
                }
            }
        }

        private IOrganizationService internalService = null;
        public IOrganizationService Service
        {
            get
            {
                if (internalService == null)
                {

                    
                    var conn = new Microsoft.Xrm.Tooling.Connector.CrmServiceClient(connectionString);
                    if (conn.OrganizationServiceProxy != null)
                    {
                        conn.OrganizationServiceProxy.Timeout = new TimeSpan(0, 20, 0);
                    }
                    internalService = (IOrganizationService)conn.OrganizationWebProxyClient != null ? (IOrganizationService)conn.OrganizationWebProxyClient : (IOrganizationService)conn.OrganizationServiceProxy;
                    
                    if (internalService == null)
                    {
                        string[] pairs = connectionString.Split(';');
                        Uri oUri = null;
                        ClientCredentials clientCredentials = new ClientCredentials();
                        

                        foreach (string p in pairs)
                        {
                            string[] keyValue = p.Trim().Split('=');
                            switch (keyValue[0].ToLower())
                            {
                                case "url":
                                    oUri = new Uri(keyValue[1]+ "/XRMServices/2011/Organization.svc");
                                    break;
                                case "domain":
                                    clientCredentials.UserName.UserName = keyValue[1] + "\\" + clientCredentials.UserName.UserName;
                                    break;
                                case "username":
                                    clientCredentials.UserName.UserName = clientCredentials.UserName.UserName + keyValue[1];
                                    break;
                                case "password":
                                    clientCredentials.UserName.Password = keyValue[1];
                                    break;
                            }
                        }

                                               
                        var service = new OrganizationServiceProxy(oUri, null, clientCredentials, null);
                        service.Timeout = new TimeSpan(0, 20, 0);
                        internalService = service;
                    }
                }
                return internalService;

            }
        }

        public void ExportSolution(string solutionName, string folder, bool managed)
        {
            ExportSolutionRequest exportSolutionRequest = new ExportSolutionRequest();
            exportSolutionRequest.Managed = managed;
            exportSolutionRequest.SolutionName = solutionName;

            ExportSolutionResponse exportSolutionResponse = (ExportSolutionResponse)Service.Execute(exportSolutionRequest);

            System.IO.Directory.CreateDirectory(folder);
            byte[] exportXml = exportSolutionResponse.ExportSolutionFile;
            string filename = solutionName + ".zip";
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, filename), exportXml);
        }


        public void ImportSolution(string fileName)
        {
            byte[] importXml = System.IO.File.ReadAllBytes(fileName);

            Guid importId = Guid.NewGuid();
            ImportSolutionRequest importSolutionRequest = new ImportSolutionRequest();
            importSolutionRequest.ImportJobId = importId;
            importSolutionRequest.CustomizationFile = importXml;
            importSolutionRequest.PublishWorkflows = true;
            importSolutionRequest.OverwriteUnmanagedCustomizations = true;
            importSolutionRequest.PublishWorkflows = true;
            importSolutionRequest.SkipProductUpdateDependencies = false;

            //ImportSolutionResponse resp = (ImportSolutionResponse)Service.Execute(importSolutionRequest);
            
            

            var requestAsync = new ExecuteAsyncRequest
            {
                Request = importSolutionRequest
            };
            Service.Execute(requestAsync);
            bool isfinished = false;
            int counter = 100;
            do
            {
                try
                {
                    var job = Service.Retrieve("importjob", importId, new ColumnSet(true));

                    if (job.Contains("completedon"))
                    {
                        string data = (string)job["data"];
                        if(data.IndexOf("result=\"failure\"") > 0)
                        {
                            throw new Exception("An error has occured while exporting the solution: " + data);
                        }
                        isfinished = true;

                    }
                }
                catch(System.ServiceModel.FaultException ex)
                {
                    counter--;
                    System.Threading.Thread.Sleep(500);
                    if (counter == 0) throw new Exception("It's taking too long to import the " + fileName);
                }
                
            } while (isfinished == false);
            
            PublishAll();
        }

        public void PublishAll()
        {
            PublishAllXmlRequest publishAll = new PublishAllXmlRequest();
            Service.Execute(publishAll);
        }

        public void DeserializeData(List<Entity> entities, bool createOnly)
        {
            foreach (var e in entities)
            {
                var metadata = ReferenceResolution.GetMetadata(Service, e.LogicalName);

                string lookupField = null;
                if (lookupField == null)
                {
                    lookupField = metadata.PrimaryIdAttribute;
                }

                ReferenceResolution.ResolveReferences(Service, e);

                if (metadata.IsIntersect == null || !metadata.IsIntersect.Value)
                {

                    if (lookupField != metadata.PrimaryIdAttribute && !e.Contains(lookupField))
                    {
                        throw new InvalidPluginExecutionException("Lookup error: The entity being imported does not have '" + lookupField + "' attribute");
                    }
                    QueryExpression qe = new QueryExpression(e.LogicalName);
                    qe.Criteria.AddCondition(new ConditionExpression(lookupField, ConditionOperator.Equal,
                        lookupField == metadata.PrimaryIdAttribute ? e.Id : e[lookupField]));

                    var existing = Service.RetrieveMultiple(qe).Entities.FirstOrDefault();
                    if (existing != null)
                    {
                        if (!createOnly)
                        {
                            e.Id = existing.Id;
                            Service.Update(e);
                        }
                    }
                    else
                    {
                        Service.Create(e);
                    }
                }
                else
                {
                    if (e.LogicalName == "listmember")
                    {
                        if (e.Contains("entityid") && e.Contains("listid"))
                        {
                            QueryExpression qe = new QueryExpression("listmember");
                            qe.Criteria.AddCondition(new ConditionExpression("entityid", ConditionOperator.Equal, ((EntityReference)e["entityid"]).Id));
                            qe.Criteria.AddCondition(new ConditionExpression("listid", ConditionOperator.Equal, ((EntityReference)e["listid"]).Id));
                            bool exists = Service.RetrieveMultiple(qe).Entities.FirstOrDefault() != null;
                            if (!exists)
                            {
                                AddMemberListRequest amlr = new AddMemberListRequest();
                                amlr.EntityId = ((EntityReference)e["entityid"]).Id;
                                amlr.ListId = ((EntityReference)e["listid"]).Id;
                                Service.Execute(amlr);
                            }
                        }
                    }
                    else
                    {
                        foreach (var r in metadata.ManyToManyRelationships)
                        {
                            if (r.IntersectEntityName == e.LogicalName)
                            {

                                if (e.Contains(r.Entity1IntersectAttribute)
                                    && e.Contains(r.Entity2IntersectAttribute)
                                    )
                                {
                                    QueryExpression qe = new QueryExpression(r.IntersectEntityName);
                                    qe.Criteria.AddCondition(new ConditionExpression(r.Entity1IntersectAttribute, ConditionOperator.Equal, (Guid)e[r.Entity1IntersectAttribute]));
                                    qe.Criteria.AddCondition(new ConditionExpression(r.Entity2IntersectAttribute, ConditionOperator.Equal, (Guid)e[r.Entity2IntersectAttribute]));
                                    bool exists = Service.RetrieveMultiple(qe).Entities.FirstOrDefault() != null;
                                    if (!exists
                                        && Common.RecordExists(Service, r.Entity1LogicalName, r.Entity1IntersectAttribute, (Guid)e[r.Entity1IntersectAttribute])
                                        && Common.RecordExists(Service, r.Entity2LogicalName, r.Entity2IntersectAttribute, (Guid)e[r.Entity2IntersectAttribute])
                                        )
                                    {

                                        Relationship rs = new Relationship(r.SchemaName);
                                        EntityReferenceCollection collection = new EntityReferenceCollection();

                                        collection.Add(new EntityReference(r.Entity2IntersectAttribute)
                                        {
                                            Id = (Guid)e[r.Entity2IntersectAttribute]
                                        });

                                        Service.Associate(r.Entity1LogicalName,
                                            (Guid)e[r.Entity1IntersectAttribute],
                                            rs,
                                            collection);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
}
