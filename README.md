local.settings.json is in git ignore so you'll need too create it

{
    "IsEncrypted": false,
    "Values": {
        //This acccount is universal for Azurite, but not Azure Storage. If you decide to use Azure Storage, you will need to change the AzureWebJobsStorage-connection string.
        "AzureWebJobsStorage": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "OpenWeatherMapApiKey": "", <-- Add your own OpenWeatherMap key
        "AzureWebJobsSecretStorageType": "files"
    }
}

The app has only been tested with Azurite emulator for local Azure Storage development, altough in theory it should work with in-cloud Azure Storage. To run it locally, install Azurite.
https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage

Start Azurite with the following command
azurite --location c:\azurite

To build the app, navigate to the project root and run the following in CMD
dotnet clean
dotnet build
func start

To retrieve logs
curl "http://localhost:7071/api/logs?from=2020-01-01&to=2030-12-31"
This will return a JSON structure containing all matching objects in the specified time period.

Each log-object will contain RowKey-property. This is the related blobs filename, or log entry Id if you will.

To retrieve the blob payload
curl "http://localhost:7071/api/payload/0814d46f-2bf5-4a36-9c79-cedf4097703d"
