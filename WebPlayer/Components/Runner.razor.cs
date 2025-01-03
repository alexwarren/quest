using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using QuestViva.PlayerCore;
using TextAdventures.Quest;

namespace WebPlayer.Components;

public partial class Runner : ComponentBase
{
    [Parameter] public required IGameDataProvider GameDataProvider { get; set; }
    [Inject] private IJSRuntime JS { get; set; } = null!;
    private QuestViva.PlayerCore.Player Player { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        
        var gameData = await GameDataProvider.GetData();
        
        // TODO: Is there a better way of getting libraryFolder?
        var game = GameLauncher.GetGame(gameData, null);
        
        Player = new QuestViva.PlayerCore.Player(game, GameDataProvider.ResourcesId, JS);
        
        GameResources.AddResourceProvider(GameDataProvider.ResourcesId, Player.GetResource);

        await Player.Initialise();
    }
}