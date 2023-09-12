// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;

namespace ServiceBusPublishSubscribeAdvanceFeatures
{
    public class Program
    {
        /**
         * Azure Service Bus basic scenario sample.
         * - Create namespace.
         * - Create a service bus subscription in the topic with session and dead-letter enabled.
         * - Create another subscription in the topic with auto deletion of idle entities.
         * - Create second topic with new Send Authorization rule, partitioning enabled and a new Service bus Subscription.
         * - Update second topic to change time for AutoDeleteOnIdle time, without Send rule and with a new manage authorization rule.
         * - Get the keys from default authorization rule to connect to topic.
         * - Delete a topic
         * - Delete namespace
         */
        private static ResourceIdentifier? _resourceGroupId = null;
        public static async Task RunSample(ArmClient client)
        {
            try
            {
                //============================================================

                // Create a namespace.
               
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the USCentral region
                var rgName = Utilities.CreateRandomName("rgSB04_");
                Utilities.Log($"creating resource group with name : {rgName} ...");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.CentralUS));
                var resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created resource group with name: " + resourceGroup.Data.Name + "...");

                //create namespace and wait for completion
                var nameSpaceName = Utilities.CreateRandomName("nameSpace");
                Utilities.Log("Creating namespace " + nameSpaceName + " in resource group " + rgName + "...");
                var namespaceCollection = resourceGroup.GetServiceBusNamespaces();
                var data = new ServiceBusNamespaceData(AzureLocation.WestUS)
                {
                    Sku = new ServiceBusSku(ServiceBusSkuName.Standard),
                };
                var serviceBusNamespace = (await namespaceCollection.CreateOrUpdateAsync(WaitUntil.Completed,nameSpaceName,data)).Value;
                Utilities.Log("Created service bus " + serviceBusNamespace.Data.Name);

                //Create a topic
                Utilities.Log("Create topic1 in namespace");
                var topicName = Utilities.CreateRandomName("topic1_");
                var topicCollection = serviceBusNamespace.GetServiceBusTopics();
                var topiccData = new ServiceBusTopicData()
                {
                    MaxSizeInMegabytes = 1024,
                };
                var topic1 = (await topicCollection.CreateOrUpdateAsync(WaitUntil.Completed,topicName,topiccData)).Value;
                Utilities.Log("Created a topic" + topic1.Data.Name);
                Utilities.Log("Created topic following topic along with namespace : " + nameSpaceName);
                
                var firstTopic = (serviceBusNamespace.GetServiceBusTopic(topicName)).Value;
                Utilities.Log(firstTopic);

                //============================================================

                // Create a service bus subscription in the topic with session and dead-letter enabled.
                var subscription1Name = Utilities.CreateRandomName("subs1_");
                Utilities.Log("Creating subscription " + subscription1Name + " in topic " + topic1.Data.Name + "...");
                var subscription1Collection = firstTopic.GetServiceBusSubscriptions();
                var subscription1Data = new ServiceBusSubscriptionData()
                {
                    RequiresSession = true,
                    DefaultMessageTimeToLive = TimeSpan.FromMinutes(20),
                    MaxDeliveryCount = 20,
                    DeadLetteringOnMessageExpiration = true,
                    DeadLetteringOnFilterEvaluationExceptions = true,
                };
                var firstSubscription = (await subscription1Collection.CreateOrUpdateAsync(WaitUntil.Completed,subscription1Name, subscription1Data)).Value;
                Utilities.Log("Created subscription " + subscription1Name + " in topic " + topic1.Data.Name + "...");
                Utilities.Log(firstSubscription);

                //============================================================

                // Create another subscription in the topic with auto deletion of idle entities.
                var subscription2Name = Utilities.CreateRandomName("subs2_");
                Utilities.Log("Creating another subscription" + subscription2Name + " in topic " + topic1.Data.Name + "...");
                var subscription2Collection = topic1.GetServiceBusSubscriptions();
                var subscription2Data = new ServiceBusSubscriptionData()
                {
                    RequiresSession = true,
                    AutoDeleteOnIdle = TimeSpan.FromMinutes(20),
                };
                var subscription2 = (await subscription2Collection.CreateOrUpdateAsync(WaitUntil.Completed, subscription2Name, subscription2Data)).Value;
                Utilities.Log("Created another subscription" + subscription2Name + " in topic " + topic1.Data.Name + "...");
                Utilities.Log(subscription2);

                //============================================================

                // Create second topic with new Send Authorization rule, partitioning enabled and a new Service bus Subscription.
                var topic2Name = Utilities.CreateRandomName("topic2_");
                Console.WriteLine("Creating second topic " + topic2Name + ", with De-duplication and AutoDeleteOnIdle features...");
                var topic2Collection = serviceBusNamespace.GetServiceBusTopics();
                var topic2Data = new ServiceBusTopicData()
                {
                    EnablePartitioning = true,
                };
                var topic2 = (await topic2Collection.CreateOrUpdateAsync(WaitUntil.Completed, topic2Name, topic2Data)).Value;
                Console.WriteLine("Created second topic :" + topic2.Data.Name);

                //Create rule
                var sendRuleName = Utilities.CreateRandomName("SendRule");
                Utilities.Log("Creating rule : " + sendRuleName + " in topic " + topic2.Data.Name + "...");
                var ruleCollection = topic2.GetServiceBusTopicAuthorizationRules();
                var sendRuleData = new ServiceBusAuthorizationRuleData()
                {
                    Rights =
                    {
                      ServiceBusAccessRight.Send
                    }
                };
                var sendRule = (await ruleCollection.CreateOrUpdateAsync(WaitUntil.Completed, sendRuleName, sendRuleData)).Value;
                Utilities.Log("Created sendrule ：" + sendRule.Data.Name);

                //Create third subscription
                var subscription3Name = Utilities.CreateRandomName("subs3_");
                Utilities.Log("Creating third subscription" + subscription3Name + " in topic " + topic2.Data.Name + "...");
                var subscription3Collection = topic2.GetServiceBusSubscriptions();
                var subscription3Data = new ServiceBusSubscriptionData()
                {
                    RequiresSession = true,
                };
                var subscription3 = (await subscription3Collection.CreateOrUpdateAsync(WaitUntil.Completed, subscription3Name, subscription3Data)).Value;
                Utilities.Log("Created third subscription" + subscription3.Data.Name);

                //List
                await foreach (var authorizationRule in ruleCollection.GetAllAsync())
                {
                    Utilities.Log(authorizationRule);
                }

                //============================================================

                // Update second topic to change time for AutoDeleteOnIdle time, without Send rule and with a new manage authorization rule.
                Utilities.Log("Updating second topic " + topic2Name + "...");
                var updateData = new ServiceBusTopicData()
                {
                    AutoDeleteOnIdle = TimeSpan.FromMinutes(5),
                };

                //Delete sendRule
                Utilities.Log("Deleting sendRule...");
                _ = await sendRule.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Deleted sendRule...");

                //Create rule
                var manageRuleName = Utilities.CreateRandomName("ManageRule");
                Utilities.Log("Creating rule" + manageRuleName + " in topic " + topic2.Data.Name + "...");
                var manageRuleData = new ServiceBusAuthorizationRuleData()
                {
                    Rights =
                    {
                        ServiceBusAccessRight.Manage,
                        ServiceBusAccessRight.Send,
                        ServiceBusAccessRight.Listen
                    }
                };
                var manageRule = (await ruleCollection.CreateOrUpdateAsync(WaitUntil.Completed, manageRuleName, manageRuleData)).Value;
                Utilities.Log("Created manageRule : " + manageRule.Data.Name);
                _ = topic2.UpdateAsync(WaitUntil.Completed,updateData);
                Utilities.Log("Updated second topic to change its auto deletion time");

                //List
                Console.WriteLine("Updated  following authorization rules in second topic, new list of authorization rules are ");
                await foreach (var authorizationRule in ruleCollection.GetAllAsync())
                {
                    Utilities.Log(authorizationRule);
                }

                //=============================================================

                // Get connection string for default authorization rule of namespace
                var count = 0;
                await foreach(var namespaceAuthorizationRule in ruleCollection.GetAllAsync())
                {
                    if(count == 0)
                    {
                        Console.WriteLine("Getting keys for authorization rule ...");
                        Utilities.Log(namespaceAuthorizationRule.GetKeys());
                        Console.WriteLine("Got keys for authorization rule ...");
                    }
                    Utilities.Log(namespaceAuthorizationRule);
                    count++;
                }
                Utilities.Log("Number of authorization rule for namespace :" + count);
                
                //=============================================================
               
                // Delete a topic and namespace
               
                Utilities.Log("Deleting topic " + topicName + "in namespace : " + serviceBusNamespace.Data.Name + "...");
                await topic1.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Deleted topic " + topicName + "...");

                Utilities.Log("Deleting namespace : " + serviceBusNamespace.Data.Name + "...");
                try
                {
                    await serviceBusNamespace.DeleteAsync(WaitUntil.Completed);
                }
                catch (Exception)
                {
                }
                Utilities.Log("Deleted namespace : " + nameSpaceName + "...");
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }
        public static async Task Main(string[] args)
        {
            try
            {
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);
                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}
