// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using System;
using System.Linq;
using System.Text;

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
        public static void RunSample(IAzure azure)
        {
            var rgName = SdkContext.RandomResourceName("rgSB04_", 24);
            var namespaceName = SdkContext.RandomResourceName("namespace", 20);
            var topic1Name = SdkContext.RandomResourceName("topic1_", 24);
            var topic2Name = SdkContext.RandomResourceName("topic2_", 24);
            var subscription1Name = SdkContext.RandomResourceName("subs_", 24);
            var subscription2Name = SdkContext.RandomResourceName("subs_", 24);
            var subscription3Name = SdkContext.RandomResourceName("subs_", 24);
            var sendRuleName = "SendRule";
            var manageRuleName = "ManageRule";

            try
            {
                //============================================================
                // Create a namespace.

                Console.WriteLine("Creating name space " + namespaceName + " in resource group " + rgName + "...");

                var serviceBusNamespace = azure.ServiceBusNamespaces
                        .Define(namespaceName)
                        .WithRegion(Region.USWest)
                        .WithNewResourceGroup(rgName)
                        .WithSku(NamespaceSku.Standard)
                        .WithNewTopic(topic1Name, 1024)
                        .Create();

                Console.WriteLine("Created service bus " + serviceBusNamespace.Name);
                PrintServiceBusNamespace(serviceBusNamespace);

                Console.WriteLine("Created topic following topic along with namespace " + namespaceName);

                var firstTopic = serviceBusNamespace.Topics.GetByName(topic1Name);
                PrintTopic(firstTopic);

                //============================================================
                // Create a service bus subscription in the topic with session and dead-letter enabled.

                Console.WriteLine("Creating subscription " + subscription1Name + " in topic " + topic1Name + "...");
                var firstSubscription = firstTopic.Subscriptions.Define(subscription1Name)
                        .WithSession()
                        .WithDefaultMessageTTL(TimeSpan.FromMinutes(20))
                        .WithMessageMovedToDeadLetterSubscriptionOnMaxDeliveryCount(20)
                        .WithExpiredMessageMovedToDeadLetterSubscription()
                        .WithMessageMovedToDeadLetterSubscriptionOnFilterEvaluationException()
                        .Create();
                Console.WriteLine("Created subscription " + subscription1Name + " in topic " + topic1Name + "...");

                PrintServiceBusSubscription(firstSubscription);

                //============================================================
                // Create another subscription in the topic with auto deletion of idle entities.
                Console.WriteLine("Creating another subscription " + subscription2Name + " in topic " + topic1Name + "...");

                var secondSubscription = firstTopic.Subscriptions.Define(subscription2Name)
                        .WithSession()
                        .WithDeleteOnIdleDurationInMinutes(20)
                        .Create();
                Console.WriteLine("Created subscription " + subscription2Name + " in topic " + topic1Name + "...");

                PrintServiceBusSubscription(secondSubscription);

                //============================================================
                // Create second topic with new Send Authorization rule, partitioning enabled and a new Service bus Subscription.

                Console.WriteLine("Creating second topic " + topic2Name + ", with De-duplication and AutoDeleteOnIdle features...");

                var secondTopic = serviceBusNamespace.Topics.Define(topic2Name)
                        .WithNewSendRule(sendRuleName)
                        .WithPartitioning()
                        .WithNewSubscription(subscription3Name)
                        .Create();

                Console.WriteLine("Created second topic in namespace");

                PrintTopic(secondTopic);

                Console.WriteLine("Creating following authorization rules in second topic ");

                var authorizationRules = secondTopic.AuthorizationRules.List();
                foreach (var authorizationRule in authorizationRules)
                {
                    PrintAuthorizationRule(authorizationRule);
                }

                //============================================================
                // Update second topic to change time for AutoDeleteOnIdle time, without Send rule and with a new manage authorization rule.
                Console.WriteLine("Updating second topic " + topic2Name + "...");

                secondTopic = secondTopic.Update()
                        .WithDeleteOnIdleDurationInMinutes(5)
                        .WithoutAuthorizationRule(sendRuleName)
                        .WithNewManageRule(manageRuleName)
                        .Apply();

                Console.WriteLine("Updated second topic to change its auto deletion time");

                PrintTopic(secondTopic);
                Console.WriteLine("Updated  following authorization rules in second topic, new list of authorization rules are ");

                authorizationRules = secondTopic.AuthorizationRules.List();
                foreach (var authorizationRule in  authorizationRules)
                {
                    PrintAuthorizationRule(authorizationRule);
                }

                //=============================================================
                // Get connection string for default authorization rule of namespace

                var namespaceAuthorizationRules = serviceBusNamespace.AuthorizationRules.List();
                Console.WriteLine("Number of authorization rule for namespace :" + namespaceAuthorizationRules.Count());


                foreach (var namespaceAuthorizationRule in  namespaceAuthorizationRules)
                {
                    PrintNamespaceAuthorizationRule(namespaceAuthorizationRule);
                }

                Console.WriteLine("Getting keys for authorization rule ...");

                var keys = namespaceAuthorizationRules.FirstOrDefault().GetKeys();
                PrintKeys(keys);

                //=============================================================
                // Delete a topic and namespace
                Console.WriteLine("Deleting topic " + topic1Name + "in namespace " + namespaceName + "...");
                serviceBusNamespace.Topics.DeleteByName(topic1Name);
                Console.WriteLine("Deleted topic " + topic1Name + "...");

                Console.WriteLine("Deleting namespace " + namespaceName + "...");
                // This will delete the namespace and topic within it.
                try
                {
                    azure.ServiceBusNamespaces.DeleteById(serviceBusNamespace.Id);
                }
                catch (Exception)
                {
                }
                Console.WriteLine("Deleted namespace " + namespaceName + "...");

            }
            finally
            {
                try
                {
                    Console.WriteLine("Deleting Resource Group: " + rgName);
                    azure.ResourceGroups.BeginDeleteByName(rgName);
                    Console.WriteLine("Deleted Resource Group: " + rgName);
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Console.WriteLine(g);
                }
            }
        }
        public static void Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

                var azure = Azure
                    .Configure()
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .Authenticate(credentials)
                    .WithDefaultSubscription();

                // Print selected subscription
                Console.WriteLine("Selected subscription: " + azure.SubscriptionId);

                RunSample(azure);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void PrintServiceBusNamespace(IServiceBusNamespace serviceBusNamespace)
        {
            var builder = new StringBuilder()
                    .Append("Service bus Namespace: ").Append(serviceBusNamespace.Id)
                    .Append("\n\tName: ").Append(serviceBusNamespace.Name)
                    .Append("\n\tRegion: ").Append(serviceBusNamespace.RegionName)
                    .Append("\n\tResourceGroupName: ").Append(serviceBusNamespace.ResourceGroupName)
                    .Append("\n\tCreatedAt: ").Append(serviceBusNamespace.CreatedAt)
                    .Append("\n\tUpdatedAt: ").Append(serviceBusNamespace.UpdatedAt)
                    .Append("\n\tDnsLabel: ").Append(serviceBusNamespace.DnsLabel)
                    .Append("\n\tFQDN: ").Append(serviceBusNamespace.Fqdn)
                    .Append("\n\tSku: ")
                    .Append("\n\t\tCapacity: ").Append(serviceBusNamespace.Sku.Capacity)
                    .Append("\n\t\tSkuName: ").Append(serviceBusNamespace.Sku.Name)
                    .Append("\n\t\tTier: ").Append(serviceBusNamespace.Sku.Tier);

            Console.WriteLine(builder.ToString());
        }

        static void PrintTopic(ITopic topic)
        {
            StringBuilder builder = new StringBuilder()
                    .Append("Service bus topic: ").Append(topic.Id)
                    .Append("\n\tName: ").Append(topic.Name)
                    .Append("\n\tResourceGroupName: ").Append(topic.ResourceGroupName)
                    .Append("\n\tCreatedAt: ").Append(topic.CreatedAt)
                    .Append("\n\tUpdatedAt: ").Append(topic.UpdatedAt)
                    .Append("\n\tAccessedAt: ").Append(topic.AccessedAt)
                    .Append("\n\tActiveMessageCount: ").Append(topic.ActiveMessageCount)
                    .Append("\n\tCurrentSizeInBytes: ").Append(topic.CurrentSizeInBytes)
                    .Append("\n\tDeadLetterMessageCount: ").Append(topic.DeadLetterMessageCount)
                    .Append("\n\tDefaultMessageTtlDuration: ").Append(topic.DefaultMessageTtlDuration)
                    .Append("\n\tDuplicateMessageDetectionHistoryDuration: ").Append(topic.DuplicateMessageDetectionHistoryDuration)
                    .Append("\n\tIsBatchedOperationsEnabled: ").Append(topic.IsBatchedOperationsEnabled)
                    .Append("\n\tIsDuplicateDetectionEnabled: ").Append(topic.IsDuplicateDetectionEnabled)
                    .Append("\n\tIsExpressEnabled: ").Append(topic.IsExpressEnabled)
                    .Append("\n\tIsPartitioningEnabled: ").Append(topic.IsPartitioningEnabled)
                    .Append("\n\tDeleteOnIdleDurationInMinutes: ").Append(topic.DeleteOnIdleDurationInMinutes)
                    .Append("\n\tMaxSizeInMB: ").Append(topic.MaxSizeInMB)
                    .Append("\n\tScheduledMessageCount: ").Append(topic.ScheduledMessageCount)
                    .Append("\n\tStatus: ").Append(topic.Status)
                    .Append("\n\tTransferMessageCount: ").Append(topic.TransferMessageCount)
                    .Append("\n\tSubscriptionCount: ").Append(topic.SubscriptionCount)
                    .Append("\n\tTransferDeadLetterMessageCount: ").Append(topic.TransferDeadLetterMessageCount);

            Console.WriteLine(builder.ToString());
        }
        static void PrintServiceBusSubscription(Microsoft.Azure.Management.ServiceBus.Fluent.ISubscription serviceBusSubscription)
        {
            StringBuilder builder = new StringBuilder()
                    .Append("Service bus subscription: ").Append(serviceBusSubscription.Id)
                    .Append("\n\tName: ").Append(serviceBusSubscription.Name)
                    .Append("\n\tResourceGroupName: ").Append(serviceBusSubscription.ResourceGroupName)
                    .Append("\n\tCreatedAt: ").Append(serviceBusSubscription.CreatedAt)
                    .Append("\n\tUpdatedAt: ").Append(serviceBusSubscription.UpdatedAt)
                    .Append("\n\tAccessedAt: ").Append(serviceBusSubscription.AccessedAt)
                    .Append("\n\tActiveMessageCount: ").Append(serviceBusSubscription.ActiveMessageCount)
                    .Append("\n\tDeadLetterMessageCount: ").Append(serviceBusSubscription.DeadLetterMessageCount)
                    .Append("\n\tDefaultMessageTtlDuration: ").Append(serviceBusSubscription.DefaultMessageTtlDuration)
                    .Append("\n\tIsBatchedOperationsEnabled: ").Append(serviceBusSubscription.IsBatchedOperationsEnabled)
                    .Append("\n\tDeleteOnIdleDurationInMinutes: ").Append(serviceBusSubscription.DeleteOnIdleDurationInMinutes)
                    .Append("\n\tScheduledMessageCount: ").Append(serviceBusSubscription.ScheduledMessageCount)
                    .Append("\n\tStatus: ").Append(serviceBusSubscription.Status)
                    .Append("\n\tTransferMessageCount: ").Append(serviceBusSubscription.TransferMessageCount)
                    .Append("\n\tIsDeadLetteringEnabledForExpiredMessages: ").Append(serviceBusSubscription.IsDeadLetteringEnabledForExpiredMessages)
                    .Append("\n\tIsSessionEnabled: ").Append(serviceBusSubscription.IsSessionEnabled)
                    .Append("\n\tLockDurationInSeconds: ").Append(serviceBusSubscription.LockDurationInSeconds)
                    .Append("\n\tMaxDeliveryCountBeforeDeadLetteringMessage: ").Append(serviceBusSubscription.MaxDeliveryCountBeforeDeadLetteringMessage)
                    .Append("\n\tIsDeadLetteringEnabledForFilterEvaluationFailedMessages: ").Append(serviceBusSubscription.IsDeadLetteringEnabledForFilterEvaluationFailedMessages)
                    .Append("\n\tTransferMessageCount: ").Append(serviceBusSubscription.TransferMessageCount)
                    .Append("\n\tTransferDeadLetterMessageCount: ").Append(serviceBusSubscription.TransferDeadLetterMessageCount);

            Console.WriteLine(builder.ToString());
        }

        static void PrintAuthorizationRule(ITopicAuthorizationRule topicAuthorizationRule)
        {
            StringBuilder builder = new StringBuilder()
                    .Append("Service bus topic authorization rule: ").Append(topicAuthorizationRule.Id)
                    .Append("\n\tName: ").Append(topicAuthorizationRule.Name)
                    .Append("\n\tResourceGroupName: ").Append(topicAuthorizationRule.ResourceGroupName)
                    .Append("\n\tNamespace Name: ").Append(topicAuthorizationRule.NamespaceName)
                    .Append("\n\tTopic Name: ").Append(topicAuthorizationRule.TopicName);

            var rights = topicAuthorizationRule.Rights;
            builder.Append("\n\tNumber of access rights in queue: ").Append(rights.Count);
            foreach (var right in rights)
            {
                builder.Append("\n\t\tAccessRight: ")
                        .Append("\n\t\t\tName :").Append(right.ToString());
            }

            Console.WriteLine(builder.ToString());
        }

        static void PrintNamespaceAuthorizationRule(INamespaceAuthorizationRule namespaceAuthorizationRule)
        {
            StringBuilder builder = new StringBuilder()
                    .Append("Service bus queue authorization rule: ").Append(namespaceAuthorizationRule.Id)
                    .Append("\n\tName: ").Append(namespaceAuthorizationRule.Name)
                    .Append("\n\tResourceGroupName: ").Append(namespaceAuthorizationRule.ResourceGroupName)
                    .Append("\n\tNamespace Name: ").Append(namespaceAuthorizationRule.NamespaceName);

            var rights = namespaceAuthorizationRule.Rights;
            builder.Append("\n\tNumber of access rights in queue: ").Append(rights.Count());
            foreach (var right in rights)
            {
                builder.Append("\n\t\tAccessRight: ")
                        .Append("\n\t\t\tName :").Append(right.ToString());
            }

            Console.WriteLine(builder.ToString());
        }

        static void PrintKeys(IAuthorizationKeys keys)
        {
            StringBuilder builder = new StringBuilder()
                    .Append("Authorization keys: ")
                    .Append("\n\tPrimaryKey: ").Append(keys.PrimaryKey)
                    .Append("\n\tPrimaryConnectionString: ").Append(keys.PrimaryConnectionString)
                    .Append("\n\tSecondaryKey: ").Append(keys.SecondaryKey)
                    .Append("\n\tSecondaryConnectionString: ").Append(keys.SecondaryConnectionString);

            Console.WriteLine(builder.ToString());
        }
    }
}
