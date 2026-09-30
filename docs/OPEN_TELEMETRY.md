OpenTelemetry setup

This repo ships a placeholder for OpenTelemetry. In local/CI environments package versions and availability may differ.

Recommended production setup (sample):

1) Add packages:
   - OpenTelemetry.Extensions.Hosting
   - OpenTelemetry.Exporter.OpenTelemetryProtocol
   - OpenTelemetry

2) Configure OTLP exporter in Program.cs (example):

```csharp
var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];
if (!string.IsNullOrEmpty(otlpEndpoint))
{
	Sdk.CreateTracerProviderBuilder()
		.AddAspNetCoreInstrumentation()
		.AddHttpClientInstrumentation()
		.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint))
		.Build();
}
```

3) Or use AddOpenTelemetryTracing extension if available:

```csharp
builder.Services.AddOpenTelemetryTracing(tp =>
{
	tp.AddAspNetCoreInstrumentation()
	  .AddHttpClientInstrumentation()
	  .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
});
```

4) For Application Insights use the Application Insights exporter or use ApplicationInsights SDK.

Notes:
- This repo currently registers Application Insights if ApplicationInsights:InstrumentationKey is present.
- If you want me to re-enable programmatic TracerProvider creation in Program.cs, I can add it once you confirm package versions to use in production.
