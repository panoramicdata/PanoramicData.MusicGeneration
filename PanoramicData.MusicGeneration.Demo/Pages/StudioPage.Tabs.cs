using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public partial class StudioPage
{
	private static readonly string[] Tabs = ["Song", "Harmony", "Expression", "Instruments", "Structure"];
	private readonly ElementReference[] tabButtons = new ElementReference[Tabs.Length];
	private string activeTab = "Song";

	private static string TabTitle(string tab) => tab switch
	{
		"Harmony" => "Harmony & melody",
		"Expression" => "Groove & expression",
		"Instruments" => "Instruments & mix",
		_ => tab
	};

	private async Task TabKeyAsync(KeyboardEventArgs args, int index)
	{
		var next = args.Key switch
		{
			"ArrowRight" => (index + 1) % Tabs.Length,
			"ArrowLeft" => (index + Tabs.Length - 1) % Tabs.Length,
			"Home" => 0,
			"End" => Tabs.Length - 1,
			_ => index
		};
		if (next == index) { return; }
		activeTab = Tabs[next];
		await tabButtons[next].FocusAsync();
	}
}
