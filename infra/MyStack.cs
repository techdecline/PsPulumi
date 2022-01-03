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
        var blobContainer = new AzureNative.Storage.BlobContainer("frontend-files", new AzureNative.Storage.BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            ResourceGroupName = resourceGroup.Name,
            PublicAccess = AzureNative.Storage.PublicAccess.Blob
        });

        // Create File Asset
        var asset = new FileAsset("../frontend/build/helloworld.html");

        // Create Storage Blob from Asset
        var storageBlob = new AzureNative.Storage.Blob("helloworld.html", new AzureNative.Storage.BlobArgs
        {
            AccountName = storageAccount.Name,
            ResourceGroupName = resourceGroup.Name,
            ContainerName = blobContainer.Name,
            Source = asset,
            ContentType = "text/html"
        });
    }
}
