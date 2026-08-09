namespace Zonit.Extensions.Website;

/// <summary>
/// The default document shell. Used by the <c>UseWebsite</c> overloads that take no root
/// component, so a Site with nothing unusual to say about its document does not have to declare
/// one.
/// </summary>
/// <remarks>
/// <para>Pure <see cref="AppBase"/> with no additions — everything it renders comes from
/// <c>SiteOptions.Document</c> and the Site's settings. Declare a subclass only when a virtual
/// needs overriding; declare a wholly different component only when the document structure
/// itself has to differ.</para>
///
/// <code>
/// app.UseWebsite("/", o =>            // no type argument, no App.razor, no subclass
/// {
///     o.Mode = WebsiteMode.Static;
///     o.Document.AddStylesheet("app.css");
/// });
/// </code>
/// </remarks>
public sealed class WebsiteApp : AppBase
{
}
