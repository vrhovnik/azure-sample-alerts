using AlertCall;
using Spectre.Console;

AnsiConsole.WriteLine("Loading alerts");
var subscriptionId = Environment.GetEnvironmentVariable("SubscriptionId") ?? "";
var clientId = Environment.GetEnvironmentVariable("ClientId") ?? "";
var secret = Environment.GetEnvironmentVariable("Secret") ?? "";
var resource = Environment.GetEnvironmentVariable("Resource") ?? "";

var table = new Table();
table.AddColumn("Subscription Id");
table.AddColumn(new TableColumn("Client Id").Centered());
table.AddColumn(new TableColumn("Secret").Centered());
table.AddColumn(new TableColumn("Resource"));
table.AddRow(subscriptionId, clientId, secret, resource);
AnsiConsole.Write(table);

AnsiConsole.WriteLine("Getting token and data");
var helper = new AzureHelper(subscriptionId, clientId, secret, resource);
var token = await helper.GetTokenAsync();
AnsiConsole.WriteLine($"Token [green]{token} has been issued");

var vmName = "lb-scale-sets_8cd3c62e";
var ruleName = $"VM-{vmName}-cpu";
var resourceGroup = "rg-lb-alerts";

table = new Table();
table.AddColumn("VM");
table.AddColumn(new TableColumn("Rule name").Centered());
table.AddColumn(new TableColumn("Resource group").Centered());
table.AddRow(vmName, ruleName, resourceGroup);
AnsiConsole.Write(table);

await helper.FireAlertAsync(token, ruleName, resourceGroup, vmName);

AnsiConsole.WriteLine($"Alert {ruleName} was created and fired from REST API");

var helperManaged = new AzureManagedHelper(subscriptionId);
var cancellationToken = new CancellationToken();
await helperManaged.TriggerAlertAsync(resourceGroup, ruleName, vmName, cancellationToken: cancellationToken);
AnsiConsole.WriteLine($"Alert {ruleName} was created and fired from managed API");