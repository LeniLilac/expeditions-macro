using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.App.Services;

public static class PlacementScreenshotCatalog
{
    private const string ResourceRoot =
        "pack://application:,,,/Resources/PlacementMaps/";

    public static BitmapImage Load(PlacementTarget target)
    {
        target.Validate();
        string file = FileName(target);
        Uri uri = new(
            $"{ResourceRoot}{file}",
            UriKind.Absolute);
        var resource =
            Application.GetResourceStream(uri);
        if (resource is null)
        {
            throw new InvalidOperationException(
                "The Fast no align screenshot for this map or act is not installed.");
        }
        using Stream stream = resource.Stream;
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption =
            BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        if (image.PixelWidth !=
                RobloxClientProfile.Width ||
            image.PixelHeight !=
                RobloxClientProfile.Height)
        {
            throw new InvalidDataException(
                $"The Fast no align screenshot '{file}' must be {RobloxClientProfile.Width} by {RobloxClientProfile.Height} pixels.");
        }
        image.Freeze();
        return image;
    }

    public static string FileName(
        PlacementTarget target)
    {
        target.Validate();
        return target.Mode switch
        {
            PlacementTargetMode.Expedition =>
                target.MapNumber switch
                {
                    0 => "expedition-school-grounds.png",
                    1 => "expedition-school-grounds.png",
                    2 => "expedition-flower-forest.png",
                    3 => "expedition-rose-kingdom.png",
                    _ => throw UnknownTarget(),
                },
            PlacementTargetMode.Challenge or
            PlacementTargetMode.Story =>
                target.MapNumber switch
                {
                    1 => "story-challenge-school-grounds.png",
                    2 => "story-challenge-flower-forest.png",
                    3 => "story-challenge-rose-kingdom.png",
                    4 => "story-challenge-fairy-king-forest.png",
                    5 => "story-challenge-kings-tomb.png",
                    _ => throw UnknownTarget(),
                },
            PlacementTargetMode.Raid =>
                target.ActNumber switch
                {
                    1 => "raid-spirit-city-act-1.png",
                    2 => "raid-spirit-city-act-2.png",
                    3 => "raid-spirit-city-act-3.png",
                    _ => throw UnknownTarget(),
                },
            PlacementTargetMode.Event =>
                target.ActNumber switch
                {
                    1 => "event-villain-invasion-act-1.png",
                    2 => "event-villain-invasion-act-2.png",
                    3 => "event-villain-invasion-act-3.png",
                    4 => throw new InvalidOperationException(
                        "The Villain Invasion Act 4 placement screenshot is not available yet."),
                    _ => throw UnknownTarget(),
                },
            _ => throw UnknownTarget(),
        };
    }

    private static InvalidOperationException UnknownTarget() =>
        new("No Fast no align screenshot is registered for this route.");
}
