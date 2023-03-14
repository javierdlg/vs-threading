﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.Threading.Analyzers;

/// <summary>
/// Verifies that types that implement vs-threading's IAsyncDisposable interface also implement System.IAsyncDisposable.
/// </summary>
public abstract class AbstractVSTHRD112ImplementSystemIAsyncDisposableAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "VSTHRD112";

    internal static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
        id: Id,
        title: new LocalizableResourceString(nameof(Strings.VSTHRD112_Title), Strings.ResourceManager, typeof(Strings)),
        messageFormat: new LocalizableResourceString(nameof(Strings.VSTHRD112_MessageFormat), Strings.ResourceManager, typeof(Strings)),
        description: new LocalizableResourceString(nameof(Strings.SystemIAsyncDisposablePackageNote), Strings.ResourceManager, typeof(Strings)),
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    private protected abstract LanguageUtils LanguageUtils { get; }

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

        context.RegisterCompilationStartAction(startCompilation =>
        {
            INamedTypeSymbol? vsThreadingAsyncDisposableType = startCompilation.Compilation.GetTypeByMetadataName(Types.IAsyncDisposable.FullName);
            INamedTypeSymbol? bclAsyncDisposableType = startCompilation.Compilation.GetTypeByMetadataName(Types.BclAsyncDisposable.FullName);
            if (vsThreadingAsyncDisposableType is object)
            {
                startCompilation.RegisterSymbolAction(Utils.DebuggableWrapper(c => this.AnalyzeType(c, vsThreadingAsyncDisposableType, bclAsyncDisposableType)), SymbolKind.NamedType);
            }
        });
    }

    private void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol vsThreadingAsyncDisposableType, INamedTypeSymbol? bclAsyncDisposableType)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;
        if (symbol.Interfaces.Contains(vsThreadingAsyncDisposableType))
        {
            if (bclAsyncDisposableType is null || !symbol.AllInterfaces.Contains(bclAsyncDisposableType))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        this.LanguageUtils.GetLocationOfBaseTypeName(symbol, vsThreadingAsyncDisposableType, context.Compilation, context.CancellationToken)));
            }
        }
    }
}
