# azure-managed-identity-auth
A minimal example showing how to protect an Azure app service using app registration and managed identity.

The Caller app has an endpoint, `/weatherforcast`, which calls the Callee's API endpoint, `/weatherforcast`, and returns this API response.

- Callee and Caller both require managed identity.
- Callee also requires an App Registration.

## Required tools

- net8.0
- [Azure Tools](https://marketplace.visualstudio.com/items?itemName=ms-vscode.vscode-node-azure-pack)

## Up and Run

### Install Dev Cert

```bash
dotnet dev-certs https --trust
```

### Callee

```bash
cd poc-callee
dotnet restore
dotnet build
dotnet run --launch-profile https
```

The app will run at https://localhost:7200.

### Caller

```bash
cd poc-caller
dotnet restore
dotnet build
dotnet run --launch-profile https
```

The app will run at https://localhost:7122.

## Azure configurations

### App Registration (Callee only)
- Create a App Registration named `poc-web-callee`
- The Client (Application) ID will be used when setting up Caller

### Caller

* Create an Azure Web app named `poc-web-calleer`, which belongs to a newly created resource group, `poc-web-caller_group`.
* Turn on **system-assigned managed identity**.
* The managed identity will be used when setting up Callee.
* Configure Environment Variables:
  * `CalleeApi`: Callee's app url e.g. https://app_name.app_region-01.azurewebsites.net
  * `DefaultScope`: *api://{Callee's client ID}/.default* e.g. `api://ecee9ced-1ac9-4657-b3be-0034a962f670/.default`

### Callee

* Create an Azure Web app named `poc-web-callee`, which belongs to a newly created resource group, `poc-web-callee_group`.
* Turn on **system-assigned managed identity**.
* Add Microsoft as the Identity Provider in **Authentication** settings.
  * Choose App Registration `poc-web-callee`
  * `Current tenant - Single tenant`
  * `Allow requests from any application (Not recommended)`
  * `Allow requests from specific identities`
    * Fill in the Caller's managed identity ID
  * Return 401 instead of 302

## Expected Results

- `poc-callee` should be able to deploy and run on `poc-web-callee`
- `poc-caller` should be able to deploy and run on `poc-web-caller`
- invoke `GET /token` in `poc-caller` should returns an access token
- invoke `GET /weatherforecast` in `poc-caller` should returns a list of data

---

## References
- https://learn.microsoft.com/en-us/azure/app-service/scenario-secure-app-authentication-app-service?tabs=workforce-configuration
- https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-provider-aad?tabs=workforce-configuration#use-a-built-in-authorization-policy
- https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-azure-webapp-using-vscode?view=aspnetcore-8.0