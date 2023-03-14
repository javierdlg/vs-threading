﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.VisualStudio.Threading.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class VSTHRD104OfferAsyncOptionAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "VSTHRD104";

    internal static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
        id: Id,
        title: new LocalizableResourceString(nameof(Strings.VSTHRD104_Title), Strings.ResourceManager, typeof(Strings)),
        messageFormat: new LocalizableResourceString(nameof(Strings.VSTHRD104_MessageFormat), Strings.ResourceManager, typeof(Strings)),
        helpLinkUri: Utils.GetHelpLink(Id),
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

        context.RegisterCodeBlockStartAction<SyntaxKind>(ctxt =>
        {
            // We want to scan invocations that occur inside internal, synchronous methods
            // for calls to JTF.Run or JT.Join.
            var methodSymbol = ctxt.OwningSymbol as IMethodSymbol;
            if (!methodSymbol.HasAsyncCompatibleReturnType() && Utils.IsPublic(methodSymbol) && !Utils.IsEntrypointMethod(methodSymbol, ctxt.SemanticModel, ctxt.CancellationToken) && !methodSymbol.HasAsyncAlternative(ctxt.CancellationToken))
            {
                var methodAnalyzer = new MethodAnalyzer();
                ctxt.RegisterSyntaxNodeAction(Utils.DebuggableWrapper(methodAnalyzer.AnalyzeInvocation), SyntaxKind.InvocationExpression);
                ctxt.RegisterSyntaxNodeAction(Utils.DebuggableWrapper(methodAnalyzer.AnalyzePropertyGetter), SyntaxKind.SimpleMemberAccessExpression);
            }
        });
    }

    private class MethodAnalyzer
    {
        private bool diagnosticReported;

        internal void AnalyzePropertyGetter(SyntaxNodeAnalysisContext context)
        {
            var memberAccessSyntax = (MemberAccessExpressionSyntax)context.Node;
            this.InspectMemberAccess(context, memberAccessSyntax, CommonInterest.SyncBlockingProperties);
        }

        internal void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;
            this.InspectMemberAccess(context, invocationExpressionSyntax.Expression as MemberAccessExpressionSyntax, CommonInterest.JTFSyncBlockers);
        }

        private void InspectMemberAccess(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax? memberAccessSyntax, IEnumerable<CommonInterest.SyncBlockingMethod> problematicMethods)
        {
            if (memberAccessSyntax is null)
            {
                return;
            }

            if (this.diagnosticReported)
            {
                // Don't report more than once per method.
                return;
            }

            if (context.Node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is object)
            {
                // We do not analyze JTF.Run inside anonymous functions because
                // they are so often used as callbacks where the signature is constrained.
                return;
            }

            if (CSharpUtils.IsWithinNameOf(context.Node as ExpressionSyntax))
            {
                // We do not consider arguments to nameof( ) because they do not represent invocations of code.
                return;
            }

            ISymbol? invokedMember = context.SemanticModel.GetSymbolInfo(memberAccessSyntax, context.CancellationToken).Symbol;
            if (invokedMember is object)
            {
                foreach (CommonInterest.SyncBlockingMethod item in problematicMethods)
                {
                    if (item.Method.IsMatch(invokedMember))
                    {
                        Location? location = memberAccessSyntax.Name.GetLocation();
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location));
                        this.diagnosticReported = true;
                    }
                }
            }
        }
    }
}
