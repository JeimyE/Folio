using System.Windows;
using System.Windows.Controls;

namespace Folio.Desktop.Controls;

/// <summary>
/// A content surface with the "Libro mayor" design system's hard-edge offset
/// shadow — a solid rectangle behind the card, not a blurred drop shadow (CSS's
/// <c>box-shadow: 4px 4px 0 &lt;color&gt;</c>, which has no single-element WPF
/// equivalent). Lookless: its default template lives in <c>Folio.CardStyles.xaml</c>,
/// keyed only by type, so it applies automatically.
/// </summary>
public sealed class OffsetShadowCard : ContentControl
{
    static OffsetShadowCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(OffsetShadowCard),
            new FrameworkPropertyMetadata(typeof(OffsetShadowCard)));
    }
}
