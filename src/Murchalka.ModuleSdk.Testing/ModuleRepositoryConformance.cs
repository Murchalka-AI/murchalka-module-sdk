using System.Text.Json.Nodes;
using Murchalka.ModuleProtocol.Contracts;
using Murchalka.ModuleProtocol.Json;

namespace Murchalka.ModuleSdk.Testing;

/// <summary>Validates a module repository against canonical Phase 0 contracts.</summary>
public sealed class ModuleRepositoryConformance
{
    private readonly CanonicalSchemaValidator _schemas;

    /// <summary>Initializes a validator using bundled canonical schemas.</summary>
    public ModuleRepositoryConformance() : this(CanonicalSchemaValidator.CreateBundled()) { }

    /// <summary>Initializes a validator using the supplied schema validator.</summary>
    public ModuleRepositoryConformance(CanonicalSchemaValidator schemas) =>
        _schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));

    /// <summary>Validates the module repository at the specified path.</summary>
    public ConformanceReport Validate(string moduleRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleRoot);
        var root = Path.GetFullPath(moduleRoot);
        var findings = new List<ConformanceFinding>();
        var manifestPath = Path.Combine(root, "murchalka.module.yaml");
        if (!File.Exists(manifestPath))
        {
            findings.Add(Error("ManifestConformance", "manifest-missing", manifestPath, "murchalka.module.yaml is required."));
            return new ConformanceReport(findings);
        }

        JsonObject manifest;
        try
        {
            manifest = StructuredDocument.Load(manifestPath).AsObject();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or YamlDotNet.Core.YamlException)
        {
            findings.Add(Error("ManifestConformance", "manifest-unreadable", manifestPath, exception.Message));
            return new ConformanceReport(findings);
        }

        AddSchemaFindings(findings, "ManifestConformance", manifestPath, _schemas.ValidateJson("module-manifest.schema.json", manifest));
        if (findings.Any(finding => finding.Severity == ConformanceSeverity.Error)) return new ConformanceReport(findings);

        ValidateVersionRanges(manifest, manifestPath, findings);
        ValidateCapabilities(root, manifest, findings);
        ValidateProtocolContributions(root, manifest, findings);
        ValidateClientExtensions(root, manifest, findings);
        ValidateReferences(root, manifest, findings);
        ValidateOptionalFallbacks(manifest, manifestPath, findings);
        ValidatePermissionCoverage(manifest, manifestPath, findings);
        return new ConformanceReport(findings);
    }

    private void ValidateCapabilities(string root, JsonObject manifest, ICollection<ConformanceFinding> findings)
    {
        var provided = manifest["provides"]?["capabilities"]?.AsArray();
        if (provided is null) return;

        var seen = new HashSet<(string Id, int Major)>();
        foreach (var node in provided)
        {
            var capability = node!.AsObject();
            var id = capability["id"]!.GetValue<string>();
            var versionText = capability["version"]!.GetValue<string>();
            var version = SemanticVersion.Parse(versionText);
            if (!seen.Add((id, version.Major)))
                findings.Add(Error("CapabilityContractConformance", "duplicate-capability-major", id, $"Capability '{id}' major {version.Major} is declared more than once."));

            var reference = capability["contract"]!.GetValue<string>();
            if (!TryResolveInsideRoot(root, reference, out var contractPath))
            {
                findings.Add(Error("CapabilityContractConformance", "unsafe-contract-path", reference, "Contract reference escapes the module root."));
                continue;
            }

            if (!File.Exists(contractPath))
            {
                findings.Add(Error("CapabilityContractConformance", "contract-missing", reference, "Declared capability contract was not found."));
                continue;
            }

            JsonObject contract;
            try
            {
                contract = StructuredDocument.Load(contractPath).AsObject();
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
            {
                findings.Add(Error("CapabilityContractConformance", "contract-unreadable", reference, exception.Message));
                continue;
            }

            AddSchemaFindings(findings, "CapabilityContractConformance", reference, _schemas.ValidateJson("capability.schema.json", contract));
            if (contract["metadata"] is JsonObject metadata)
            {
                Compare(metadata, "id", id, reference, "CapabilityContractConformance", "contract-metadata-mismatch", "Contract", findings);
                Compare(metadata, "version", versionText, reference, "CapabilityContractConformance", "contract-metadata-mismatch", "Contract", findings);
                Compare(metadata, "category", capability["category"]!.GetValue<string>(), reference, "CapabilityContractConformance", "contract-metadata-mismatch", "Contract", findings);
            }

            foreach (var direction in new[] { "request", "response" })
            {
                var schemaReference = contract[direction]?["schema"]?.GetValue<string>();
                if (schemaReference is null) continue;
                var contractDirectory = Path.GetDirectoryName(contractPath)!;
                if (!TryResolveInsideRoot(root, Path.GetRelativePath(root, Path.Combine(contractDirectory, schemaReference)), out var schemaPath) || !File.Exists(schemaPath))
                    findings.Add(Error("CapabilityContractConformance", $"{direction}-schema-missing", schemaReference, $"Capability {direction} schema was not found inside the module."));
            }
        }
    }

    private static void ValidateProtocolContributions(string root, JsonObject manifest, ICollection<ConformanceFinding> findings)
    {
        if (manifest["contributes"]?["protocols"] is not JsonArray protocols) return;
        var capabilities = manifest["provides"]?["capabilities"]?.AsArray()
            .Select(value => value!["id"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in protocols)
        {
            var contribution = node!.AsObject();
            var id = contribution["id"]!.GetValue<string>();
            var route = contribution["routeNamespace"]!.GetValue<string>();
            var handler = contribution["handler"]!.GetValue<string>();
            var descriptor = contribution["descriptor"]!.GetValue<string>();
            if (!routes.Add(route))
                findings.Add(Error("ProtocolContributionConformance", "duplicate-route-namespace", route, $"Protocol route namespace '{route}' is declared more than once."));
            if (!capabilities.Contains(handler))
                findings.Add(Error("ProtocolContributionConformance", "handler-capability-undeclared", id, $"Protocol handler '{handler}' is not declared by this module."));
            if (!TryResolveInsideRoot(root, descriptor, out var descriptorPath) || !File.Exists(descriptorPath))
                findings.Add(Error("ProtocolContributionConformance", "descriptor-missing", descriptor, "Protocol discovery descriptor was not found inside the module."));
        }
    }

    private static void ValidateReferences(string root, JsonObject manifest, ICollection<ConformanceFinding> findings)
    {
        foreach (var reference in EnumerateFileReferences(manifest))
        {
            if (!TryResolveInsideRoot(root, reference, out _))
                findings.Add(Error("ManifestConformance", "unsafe-reference-path", reference, "Referenced path escapes the module root."));
        }
    }

    private void ValidateClientExtensions(string root, JsonObject manifest, ICollection<ConformanceFinding> findings)
    {
        if (manifest["artifacts"]?["client"] is not JsonArray artifacts) return;
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in artifacts)
        {
            var artifact = node!.AsObject();
            var id = artifact["id"]!.GetValue<string>();
            artifactIds.Add(id);
            var reference = artifact["entrypoint"]!.GetValue<string>();
            if (!TryResolveInsideRoot(root, reference, out var path) || !File.Exists(path))
            {
                findings.Add(Error("ClientExtensionConformance", "client-artifact-missing", reference, "Declared client extension artifact was not found."));
                continue;
            }

            JsonObject envelope;
            try { envelope = StructuredDocument.Load(path).AsObject(); }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
            {
                findings.Add(Error("ClientExtensionConformance", "client-artifact-unreadable", reference, exception.Message));
                continue;
            }
            AddSchemaFindings(findings, "ClientExtensionConformance", reference, _schemas.ValidateJson("client-extension.schema.json", envelope));
            if (envelope["extension"] is JsonObject extension)
            {
                Compare(extension, "id", artifact["extensionId"]!.GetValue<string>(), reference, "ClientExtensionConformance", "client-metadata-mismatch", "Client Extension", findings);
                Compare(extension, "version", artifact["extensionVersion"]!.GetValue<string>(), reference, "ClientExtensionConformance", "client-metadata-mismatch", "Client Extension", findings);
                Compare(extension, "mode", artifact["mode"]!.GetValue<string>(), reference, "ClientExtensionConformance", "client-metadata-mismatch", "Client Extension", findings);
                Compare(extension, "fallbackComponent", artifact["fallbackComponent"]!.GetValue<string>(), reference, "ClientExtensionConformance", "client-metadata-mismatch", "Client Extension", findings);
            }
        }

        if (manifest["contributes"]?["ui"]?["components"] is not JsonArray components) return;
        foreach (var node in components)
        {
            var component = node!.AsObject();
            var artifactId = component["artifact"]!.GetValue<string>();
            if (!artifactIds.Contains(artifactId))
                findings.Add(Error("ClientExtensionConformance", "ui-artifact-undeclared", artifactId, "UI component references an undeclared client artifact."));
            if (component["schema"] is JsonValue legacySchema)
            {
                ValidateUiSchema(root, legacySchema.GetValue<string>(), "properties", findings);
                continue;
            }

            ValidateUiSchema(root, component["propertiesSchema"]!.GetValue<string>(), "properties", findings);
            ValidateUiSchema(root, component["eventsSchema"]!.GetValue<string>(), "events", findings);
        }
    }

    private static void ValidateUiSchema(string root, string reference, string kind, ICollection<ConformanceFinding> findings)
    {
        if (!TryResolveInsideRoot(root, reference, out var schemaPath) || !File.Exists(schemaPath))
        {
            findings.Add(Error("ClientExtensionConformance", $"ui-{kind}-schema-missing", reference, $"UI component {kind} schema was not found."));
            return;
        }

        try
        {
            var schema = StructuredDocument.Load(schemaPath).AsObject();
            if (schema["$schema"]?.GetValue<string>() != "https://json-schema.org/draft/2020-12/schema" || schema["type"] is null && schema["oneOf"] is null)
                findings.Add(Error("ClientExtensionConformance", $"ui-{kind}-schema-invalid", reference, $"UI component {kind} schema must declare JSON Schema 2020-12 and a root type or oneOf."));
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            findings.Add(Error("ClientExtensionConformance", $"ui-{kind}-schema-invalid", reference, exception.Message));
        }
    }

    private static IEnumerable<string> EnumerateFileReferences(JsonObject manifest)
    {
        if (manifest["configuration"]?["schema"] is JsonValue configurationSchema) yield return configurationSchema.GetValue<string>();
        if (manifest["configuration"]?["defaults"] is JsonValue defaults) yield return defaults.GetValue<string>();
        if (manifest["artifacts"]?["client"] is JsonArray clientArtifacts)
            foreach (var artifact in clientArtifacts) yield return artifact!["entrypoint"]!.GetValue<string>();
        if (manifest["contributes"]?["ui"]?["components"] is JsonArray components)
        {
            foreach (var component in components)
            {
                if (component!["schema"] is JsonValue schema) yield return schema.GetValue<string>();
                if (component["propertiesSchema"] is JsonValue propertiesSchema) yield return propertiesSchema.GetValue<string>();
                if (component["eventsSchema"] is JsonValue eventsSchema) yield return eventsSchema.GetValue<string>();
            }
        }
        if (manifest["contributes"]?["protocols"] is JsonArray protocols)
            foreach (var protocol in protocols) yield return protocol!["descriptor"]!.GetValue<string>();
        if (manifest["contributes"]?["events"] is JsonObject events)
        {
            foreach (var collection in new[] { "subscriptions", "publications" })
                if (events[collection] is JsonArray items)
                    foreach (var item in items) yield return item!["schema"]!.GetValue<string>();
        }
    }

    private static void ValidateVersionRanges(JsonObject manifest, string location, ICollection<ConformanceFinding> findings)
    {
        if (manifest["compatibility"] is JsonObject compatibility)
        {
            foreach (var pair in compatibility.Where(pair => pair.Key != "moduleProtocol"))
                TryRange(pair.Value?.GetValue<string>(), $"compatibility.{pair.Key}");
        }

        foreach (var group in new[] { "requires", "conflicts" })
            if (manifest[group]?["modules"] is JsonArray modules)
                foreach (var module in modules) TryRange(module!["version"]?.GetValue<string>(), $"{group}.modules.version");

        foreach (var group in new[] { "requires", "optional" })
            if (manifest[group]?["capabilities"] is JsonArray capabilities)
                foreach (var capability in capabilities) TryRange(capability!["version"]?.GetValue<string>(), $"{group}.capabilities.version");

        void TryRange(string? expression, string field)
        {
            if (expression is null) return;
            try { _ = VersionRangeExpression.Parse(expression); }
            catch (FormatException exception) { findings.Add(Error("CompatibilityConformance", "invalid-version-range", location, $"{field}: {exception.Message}")); }
        }
    }

    private static void ValidateOptionalFallbacks(JsonObject manifest, string location, ICollection<ConformanceFinding> findings)
    {
        if (manifest["optional"]?["capabilities"] is not JsonArray optional) return;
        foreach (var capability in optional)
        {
            var requirement = capability!.AsObject();
            if (requirement["fallback"] is null)
                findings.Add(Error("LifecycleConformance", "optional-fallback-missing", location, $"Optional requirement '{requirement["requirementId"]}' has no named fallback."));
        }
    }

    private static void ValidatePermissionCoverage(JsonObject manifest, string location, ICollection<ConformanceFinding> findings)
    {
        var requests = new HashSet<string>(StringComparer.Ordinal);
        if (manifest["permissions"]?["capabilities"]?["invoke"] is JsonArray invocations)
        {
            foreach (var invocation in invocations)
                requests.Add(invocation is JsonValue value ? value.GetValue<string>() : $"category:{invocation!["category"]!.GetValue<string>()}");
        }

        foreach (var group in new[] { "requires", "optional" })
        {
            if (manifest[group]?["capabilities"] is not JsonArray requirements) continue;
            foreach (var requirement in requirements)
            {
                var item = requirement!.AsObject();
                var selector = item["capability"] is JsonValue capability
                    ? capability.GetValue<string>()
                    : $"category:{item["category"]!.GetValue<string>()}";
                if (!requests.Contains(selector))
                    findings.Add(Error("PermissionConformance", "invoke-permission-missing", location, $"Requirement '{item["requirementId"]}' is not covered by requested invoke permissions."));
            }
        }
    }

    private static void Compare(JsonObject metadata, string field, string expected, string location, string check, string rule, string subject, ICollection<ConformanceFinding> findings)
    {
        var actual = metadata[field]?.GetValue<string>();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            findings.Add(Error(check, rule, location, $"{subject} {field} '{actual}' does not match manifest '{expected}'."));
    }

    private static bool TryResolveInsideRoot(string root, string reference, out string path)
    {
        path = Path.GetFullPath(reference, root);
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            return false;

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo? item = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : File.Exists(current) ? new FileInfo(current) : null;
            if (item?.LinkTarget is not null) return false;
        }

        return true;
    }

    private static void AddSchemaFindings(ICollection<ConformanceFinding> findings, string check, string location, SchemaValidationReport report)
    {
        foreach (var violation in report.Violations)
            findings.Add(Error(check, $"schema-{violation.Keyword}", $"{location}{violation.InstanceLocation}", violation.Message));
    }

    private static ConformanceFinding Error(string check, string code, string location, string message) =>
        new(check, code, ConformanceSeverity.Error, location, message);
}
