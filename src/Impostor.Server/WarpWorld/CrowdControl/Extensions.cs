using System.Reflection;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;

namespace Impostor.Server.WarpWorld.CrowdControl;

internal static class Extensions
{
    private static PropertyInfo game_gameState;

    static Extensions()
    {
        game_gameState = typeof(Game).GetProperty("GameState");
    }

    public static bool TryFind(this GameManager gameManager, GameCode code, out Game? game)
        => (game = gameManager.Find(code)) != null;

    public static void FlagGameAsDestroyed(this Game game) =>
        game_gameState.SetValue(game, GameStates.Destroyed, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);

    public static GameOptionsData Clone(this GameOptionsData data)
    {
        GameOptionsData result = new GameOptionsData();
        PropertyCopier<GameOptionsData>.Copy(data, result);
        return result;
    }

    private class PropertyCopier<T>
    {
        public static void Copy(T source, T dest)
        {
            var parentProperties = source.GetType().GetProperties();
            var childProperties = dest.GetType().GetProperties();

            foreach (var parentProperty in parentProperties)
            {
                foreach (var childProperty in childProperties)
                {
                    if (parentProperty.Name == childProperty.Name && parentProperty.PropertyType == childProperty.PropertyType)
                    {
                        childProperty.SetValue(dest, parentProperty.GetValue(source));
                        break;
                    }
                }
            }
        }
    }
}
