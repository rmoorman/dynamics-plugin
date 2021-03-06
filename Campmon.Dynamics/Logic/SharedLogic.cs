﻿using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;
using createsend_dotnet;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Metadata;
using System.Linq;

namespace Campmon.Dynamics.Logic
{
    public class SharedLogic
    {
        static Dictionary<string, string> schemaToDisplayName = null;
        static AttributeMetadata[] contactAttributes = null;

        public static string GetPrimaryEmailField(SubscriberEmailValues val)
        {
            // get corresponding email field from contact entity based on value of optionset from config
            if (Enum.IsDefined(typeof(SubscriberEmailValues), val))
            {
                return val.ToString().ToLower();
            }

            return string.Empty;
        }

        public static List<SubscriberCustomField> ContactAttributesToSubscriberFields(IOrganizationService orgService, ITracingService tracer, Entity contact, ICollection<String> attributes)
        {
            MetadataHelper metadataHelper = new MetadataHelper(orgService, tracer);

            var fields = new List<SubscriberCustomField>();
            foreach (var field in attributes)
            {
                if (!contact.Attributes.Contains(field))
                {                  
                    continue;
                }
                
                ///tracer.Trace(string.Format("Field: {0} exists on contact as Type: {1}", field, contact[field].GetType()));
                
                if (contact[field] is EntityReference)
                {                                        
                    // To transform Lookup and Option Set fields, use the text label and send as text
                    var refr = (EntityReference)contact[field];
                    var displayName = refr.Name;

                    // if name is empty, retrieve the entity and get it's primary attribute
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        var primaryAttrib = metadataHelper.GetEntityPrimaryAttribute(refr.LogicalName);
                        var entity = orgService.Retrieve(refr.LogicalName, refr.Id, new ColumnSet(primaryAttrib));
                        displayName = entity.GetAttributeValue<String>(primaryAttrib);
                    }

                    fields.Add(new SubscriberCustomField { Key = field, Value = displayName });
                }
                else if (contact[field] is OptionSetValue)
                {                   
                    var optionValue = (OptionSetValue)contact[field];
                    var optionLabel = metadataHelper.GetOptionSetValueLabel("contact", field, optionValue.Value);
                    fields.Add(new SubscriberCustomField { Key = field, Value = optionLabel });
                }
                else if (contact[field] is DateTime)
                {
                    // To transform date fields, send as date
                    var date = (DateTime)contact[field];
                    fields.Add(new SubscriberCustomField { Key = field, Value = date.ToString("yyyy/MM/dd") });
                }
                else if (contact[field] is Money)
                {
                    var mon = (Money)contact[field];
                    fields.Add(new SubscriberCustomField { Key = field, Value = mon.Value.ToString() });
                }
                else if (IsNumeric(contact[field]))
                {
                    // To transform numeric fields, send as number
                    fields.Add(new SubscriberCustomField { Key = field, Value = contact[field].ToString() });
                }
                else
                {                    
                    // For any other fields, send as text
                    fields.Add(new SubscriberCustomField { Key = field, Value = contact[field].ToString() });
                }
            }

            return fields;
        }

        public static bool CheckEmailIsDuplicate(IOrganizationService orgService, CampaignMonitorConfiguration config, string primaryEmailField, string email)
        {
            QueryExpression query = new QueryExpression("contact");
            QueryExpression configFilter = null;
            if (config.SyncViewId != null && config.SyncViewId != Guid.Empty)
            {
                configFilter = GetConfigFilterQuery(orgService, config.SyncViewId);
                query.Criteria = configFilter.Criteria;
            }
            else
            {
                // if no filter on query then only select active contacts
                query.Criteria.AddCondition(new ConditionExpression("statecode", ConditionOperator.Equal, 0));
            }
                      
            query.Criteria.AddCondition(new ConditionExpression(primaryEmailField, ConditionOperator.Equal, email));
            query.ColumnSet.AddColumn("contactid");
            query.TopCount = 2;
            
            return orgService.RetrieveMultiple(query).TotalRecordCount > 1;
        }

        public static QueryExpression GetConfigFilterQuery(IOrganizationService orgService, Guid viewId)
        {
            ColumnSet cols = new ColumnSet("fetchxml");
            Entity view = orgService.Retrieve("savedquery", viewId, cols);

            if (view == null || !view.Contains("fetchxml"))
            {
                return null;
            }

            var fetchRequest = new FetchXmlToQueryExpressionRequest
            {
                FetchXml = view["fetchxml"].ToString()
            };

            var queryResponse = (FetchXmlToQueryExpressionResponse)orgService.Execute(fetchRequest);
            var query = queryResponse.Query;

            return query;
        }

        private static bool IsNumeric(object Expression)
        {
            double retNum;
            bool isNum = Double.TryParse(Convert.ToString(Expression), System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
            return isNum;
        }

        public static List<SubscriberCustomField> PrettifySchemaNames(MetadataHelper metadataHelper, List<SubscriberCustomField> fields)
        {
            // convert each field to Campaign Monitor custom 
            // field names by using the display name for the field

            if (schemaToDisplayName == null)
            {
                schemaToDisplayName = new Dictionary<string, string>();
            }

            foreach (var field in fields)
            {
                if (!schemaToDisplayName.ContainsKey("contact" + field.Key))
                {
                    if (contactAttributes == null)
                    {
                        contactAttributes = metadataHelper.GetEntityAttributes("contact");
                    }

                    var displayName = (from x in contactAttributes where x.LogicalName == field.Key select x.DisplayName).FirstOrDefault();
                    if (displayName.UserLocalizedLabel != null && displayName.UserLocalizedLabel.Label != null)
                    {
                        schemaToDisplayName["contact" + field.Key] = displayName.UserLocalizedLabel.Label.ToString().Replace("/", "").Replace("\\", "");

                    }
                }

                // if label for whatever reason is null above it won't contain the key
                if (schemaToDisplayName.ContainsKey("contact" + field.Key))
                {
                    field.Key = schemaToDisplayName["contact" + field.Key];
                }                    
            }

            return fields;
        }
    }
}
