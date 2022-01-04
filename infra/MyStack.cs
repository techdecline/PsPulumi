using System.IO;
using Pulumi;
using AzureNative = Pulumi.AzureNative;

class MyStack : Stack
{
    public MyStack()
    {
        // Create an Azure Resource Group
        var resourceGroup = new AzureNative.Resources.ResourceGroup("resourceGroup");

        // Create Storage Account
        var storageAccount = new AzureNative.Storage.StorageAccount("storageaccount", new AzureNative.Storage.StorageAccountArgs
        {
            EnableHttpsTrafficOnly = true,
            Kind = AzureNative.Storage.Kind.StorageV2,
            ResourceGroupName = resourceGroup.Name,
            Sku = new AzureNative.Storage.Inputs.SkuArgs
            {
                Name = "Standard_LRS",
            },
        });

        // Create Storage Container
        var blobContainer = new AzureNative.Storage.BlobContainer("function-code", new AzureNative.Storage.BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            ResourceGroupName = resourceGroup.Name,
            PublicAccess = AzureNative.Storage.PublicAccess.Blob
        });

        var functionFileArchive = new FileArchive("../backend/handleSignup");
        var functionCodeBlob = new AzureNative.Storage.Blob("function-code", new AzureNative.Storage.BlobArgs
        {
            AccountName = storageAccount.Name,
            ResourceGroupName = resourceGroup.Name,
            ContainerName = blobContainer.Name,
            Source = functionFileArchive,
            BlobName = "source"
        });

        var servicePlan = new AzureNative.Web.AppServicePlan("windows-plan", new AzureNative.Web.AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new AzureNative.Web.Inputs.SkuDescriptionArgs
            {
                Size = "Y1",
                Tier = "Dynamic",
                Name = "Y1"
            }
        });

        var codeBlobUrl = SignedBlobReadUrl(functionCodeBlob, blobContainer, storageAccount, resourceGroup);

        var webAppFunction = new AzureNative.Web.WebApp("functionApp", new AzureNative.Web.WebAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = servicePlan.Id,
            Kind = "FunctionApp",
            SiteConfig = new AzureNative.Web.Inputs.SiteConfigArgs
            {
                AppSettings = new[]
                {
                    new AzureNative.Web.Inputs.NameValuePairArgs{
                        Name = "runtime",
                        Value = "node",
                    },
                    new AzureNative.Web.Inputs.NameValuePairArgs{
                        Name = "WEBSITE_NODE_DEFAULT_VERSION",
                        Value = "~12",
                    },
                    new AzureNative.Web.Inputs.NameValuePairArgs{
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~3",
                    },
                    new AzureNative.Web.Inputs.NameValuePairArgs{
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "node",
                    },
                    new AzureNative.Web.Inputs.NameValuePairArgs{
                        Name = "WEBSITE_RUN_FROM_PACKAGE",
                        Value = codeBlobUrl,
                    },
                },
            }
        });

        var storageAccountStaticWebsite = new AzureNative.Storage.StorageAccountStaticWebsite("staticWebsite", new AzureNative.Storage.StorageAccountStaticWebsiteArgs
        {
            AccountName = storageAccount.Name,
            ResourceGroupName = resourceGroup.Name,
            IndexDocument = "index.html"
        });

        var frontendBuildOutputFolder = "../frontend/build";
        var files = Directory.GetFiles(frontendBuildOutputFolder, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var fileName = Path.GetRelativePath(frontendBuildOutputFolder, file);
            string contentType = (new FileInfo(fileName)).Extension switch
            {
                ".css" => "text/css; charset=utf-8",
                ".html" => "text/html",
                _ => ""
            };

            new AzureNative.Storage.Blob(fileName, new AzureNative.Storage.BlobArgs
            {
                AccountName = storageAccount.Name,
                ResourceGroupName = resourceGroup.Name,
                ContainerName = storageAccountStaticWebsite.ContainerName,
                Source = new FileAsset(file),
                BlobName = fileName,
                ContentType = contentType
            });
        }

        this.Endpoint = webAppFunction.DefaultHostName;
    }

    [Output] public Output<string> Endpoint { get; set; }

    private static Output<string> SignedBlobReadUrl(AzureNative.Storage.Blob blob, AzureNative.Storage.BlobContainer container, AzureNative.Storage.StorageAccount account, AzureNative.Resources.ResourceGroup resourceGroup)
    {
        var serviceSasToken = AzureNative.Storage.ListStorageAccountServiceSAS.Invoke(new AzureNative.Storage.ListStorageAccountServiceSASInvokeArgs
        {
            AccountName = account.Name,
            Protocols = AzureNative.Storage.HttpProtocol.Https,
            SharedAccessStartTime = "2021-01-01",
            SharedAccessExpiryTime = "2030-01-01",
            Resource = AzureNative.Storage.SignedResource.C,
            ResourceGroupName = resourceGroup.Name,
            Permissions = AzureNative.Storage.Permissions.R,
            CanonicalizedResource = Output.Format($"/blob/{account.Name}/{container.Name}"),
            ContentType = "application/json",
            CacheControl = "max-age=5",
            ContentDisposition = "inline",
            ContentEncoding = "deflate",
        }).Apply(blobSAS => blobSAS.ServiceSasToken);

        return Output.Format($"https://{account.Name}.blob.core.windows.net/{container.Name}/{blob.Name}?{serviceSasToken}");
    }
}
