using Microsoft.Extensions.Logging;
using SleeveArchive.Services;
using CommunityToolkit.Maui;

namespace SleeveArchive;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<MusicBrainzService>();
		builder.Services.AddSingleton<FolderPickerService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging
			.AddDebug()
			.SetMinimumLevel(LogLevel.Debug);  // Add this for more detailed logging
#endif

		return builder.Build();
	}
}
