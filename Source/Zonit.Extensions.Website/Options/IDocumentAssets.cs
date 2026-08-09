using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;

namespace Zonit.Extensions.Website;

/// <summary>
/// The additive half of <see cref="DocumentOptions"/> — everything an area may contribute to the
/// document shell of a Site it is mounted on, and nothing else.
/// </summary>
/// <remarks>
/// <para><b>Why a narrower type than <see cref="DocumentOptions"/>.</b> An area is a guest on
/// somebody else's Site, and a guest adds to the document; it does not decide what the document
/// <em>is</em>. <see cref="DocumentOptions.Favicon"/>, <see cref="DocumentOptions.ImportMap"/>,
/// <see cref="DocumentOptions.ScopedStyles"/> and <see cref="DocumentOptions.DefaultLayoutKey"/>
/// are Site-wide verdicts: the last area to touch one silently wins, the outcome depends on mount
/// order, and the host that "configured" the value never sees it change. Handing areas an
/// append-only surface makes that class of bug unrepresentable instead of merely discouraged.</para>
///
/// <para><b>Ordering.</b> Contributions are applied at mount time, after the Site's own
/// declarations and in area registration order. Base stylesheets therefore belong to the Site and
/// an area's sheet layers on top of them — the order a plug-in wants when it restyles shared
/// chrome. Note the corollary: a Site cannot out-cascade an area it mounts, so anything the host
/// insists on winning should be specific enough not to depend on source order.</para>
/// </remarks>
public interface IDocumentAssets
{
    /// <inheritdoc cref="DocumentOptions.AddStylesheet"/>
    IDocumentAssets AddStylesheet(string url, bool fingerprint = true);

    /// <inheritdoc cref="DocumentOptions.AddScript"/>
    IDocumentAssets AddScript(string url, bool defer = false, bool async = false, bool fingerprint = true);

    /// <inheritdoc cref="DocumentOptions.AddMeta"/>
    IDocumentAssets AddMeta(string name, string content, bool isProperty = false);

    /// <inheritdoc cref="DocumentOptions.AddPreconnect"/>
    IDocumentAssets AddPreconnect(string origin, bool crossOrigin = false);

    /// <inheritdoc cref="DocumentOptions.AddHeadComponent{T}"/>
    IDocumentAssets AddHeadComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent;

    /// <inheritdoc cref="DocumentOptions.AddBodyEndComponent{T}"/>
    IDocumentAssets AddBodyEndComponent<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent;

    /// <inheritdoc cref="DocumentOptions.AddHeadContent"/>
    IDocumentAssets AddHeadContent(RenderFragment fragment);

    /// <inheritdoc cref="DocumentOptions.AddBodyEndContent"/>
    IDocumentAssets AddBodyEndContent(RenderFragment fragment);
}
