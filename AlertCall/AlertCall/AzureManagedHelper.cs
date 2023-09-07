using System.Xml;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Monitor.Models;
using Azure.ResourceManager.Resources;

namespace AlertCall;

public class AzureManagedHelper
{
    private readonly string subscriptionId;

    public AzureManagedHelper(string subscriptionId)
    {
        this.subscriptionId = subscriptionId;
    }

    public async Task<bool> TriggerAlertAsync(string resourceGroupName, string ruleName, string vmName,
        string azureLocation = "West Europe", CancellationToken cancellationToken = default)
    {
        var cred = new DefaultAzureCredential();
        var client = new ArmClient(cred);

        var resourceGroupResourceId = ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName);
        var resourceGroupResource = client.GetResourceGroupResource(resourceGroupResourceId);

        var collection = resourceGroupResource.GetAlertRules();

        var data = new AlertRuleData(new AzureLocation(azureLocation), ruleName,
            true, new ThresholdRuleCondition(MonitorConditionOperator.GreaterThan, 20)
            {
                WindowSize = XmlConvert.ToTimeSpan("PT5M"),
                TimeAggregation = ThresholdRuleConditionTimeAggregationType.Average,
                DataSource = new RuleMetricDataSource
                {
                    MetricName = "Percentage CPU",
                    ResourceId = new ResourceIdentifier(
                        $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}")
                }
            })
        {
            Description = $"rule from command line for {subscriptionId} in rg {resourceGroupName} on {vmName}",
            Actions = { },
            Tags = { }
        };
        var lro = await collection.CreateOrUpdateAsync(WaitUntil.Completed, ruleName, data, cancellationToken);
        var result = lro.Value;

        var resourceData = result.Data;
        Console.WriteLine($"Succeeded on id: {resourceData.Id}");
        return true;
    }
}