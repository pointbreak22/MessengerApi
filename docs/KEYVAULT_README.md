Key Vault integration

This project supports optional Azure Key Vault integration. To enable:

1. Set configuration key `KeyVault:Endpoint` to your Key Vault URI, e.g. `https://myvault.vault.azure.net/`.
2. The application will attempt to use DefaultAzureCredential (Managed Identity in Azure or developer credential locally).
3. Store secrets (Azure:SignalR:ConnectionString, Azure:Blob:ConnectionString, Redis:Configuration, ApplicationInsights:InstrumentationKey) in Key Vault and reference them by name.

Notes:
- For local development, you can use `az login` and `az account get-access-token` or Azure CLI authentication.
- Ensure the application's Managed Identity has `get` permission for secrets in Key Vault.
