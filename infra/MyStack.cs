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

        // // Create Storage Container
        // var blobContainer = new AzureNative.Storage.BlobContainer("frontend-files", new AzureNative.Storage.BlobContainerArgs
        // {
        //     AccountName = storageAccount.Name,
        //     ResourceGroupName = resourceGroup.Name,
        //     PublicAccess = AzureNative.Storage.PublicAccess.Blob
        // });

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
    }
}
