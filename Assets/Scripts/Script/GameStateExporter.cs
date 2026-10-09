using System;
using System.Collections.Generic;
using System.Text;

//Record of every effect activated during the game, as CEntity_EffectController only remembers the uses of the current turn
public static class EffectHistory
{
    public class Entry
    {
        public int Turn;
        public Player Player;
        public CardSource Card;
        public string EffectName;
    }

    public static List<Entry> Entries { get; private set; } = new List<Entry>();

    public static void Clear()
    {
        Entries = new List<Entry>();
    }

    public static void Record(ICardEffect cardEffect)
    {
        if (cardEffect == null || cardEffect.EffectSourceCard == null)
        {
            return;
        }

        if (GManager.instance == null || GManager.instance.turnStateMachine == null)
        {
            return;
        }

        Entries.Add(new Entry()
        {
            Turn = GManager.instance.turnStateMachine.TurnCount,
            Player = cardEffect.EffectSourceCard.Owner,
            Card = cardEffect.EffectSourceCard,
            EffectName = cardEffect.EffectName,
        });
    }
}

//Builds a Markdown summary of the game as the local player sees it (no hidden information of the opponent)
public static class GameStateExporter
{
    const string Indent = "    ";

    static readonly EffectTiming[] _effectTimings = (EffectTiming[])Enum.GetValues(typeof(EffectTiming));

    public static string Export()
    {
        TurnStateMachine turnStateMachine = GManager.instance.turnStateMachine;
        GameContext gameContext = turnStateMachine.gameContext;

        List<Player> players = new List<Player>();

        foreach (Player player in gameContext.Players)
        {
            if (player == null)
            {
                continue;
            }

            if (player.isYou)
            {
                players.Insert(0, player);
            }

            else
            {
                players.Add(player);
            }
        }

        StringBuilder text = new StringBuilder();

        text.AppendLine("# DCGO game state");
        text.AppendLine();
        text.AppendLine($"- Turn: {turnStateMachine.TurnCount}");

        if (gameContext.TurnPlayer != null)
        {
            text.AppendLine($"- Turn player: {PlayerLabel(gameContext.TurnPlayer)}");
        }

        text.AppendLine($"- Phase: {gameContext.TurnPhase}");

        if (players.Count >= 1)
        {
            int memory = players[0].MemoryForPlayer;

            if (memory == 0)
            {
                text.AppendLine("- Memory: 0");
            }

            else
            {
                Player memoryPlayer = memory > 0 ? players[0] : players[0].Enemy;

                text.AppendLine($"- Memory: {Math.Abs(memory)} on the side of {PlayerLabel(memoryPlayer)}");
            }
        }

        foreach (Player player in players)
        {
            AppendPlayer(text, player, turnStateMachine.TurnCount);
        }

        return text.ToString().TrimEnd();
    }

    #region Player
    static void AppendPlayer(StringBuilder text, Player player, int turnCount)
    {
        List<CardSource> faceUpSecurityCards = player.SecurityCards.Filter(cardSource => cardSource != null && cardSource.IsFaceUp);

        text.AppendLine();
        text.AppendLine($"## {PlayerLabel(player)}");
        text.AppendLine();
        text.AppendLine($"- Deck: {player.LibraryCards.Count}");
        text.AppendLine($"- Digi-Egg deck: {player.DigitamaLibraryCards.Count}");
        text.AppendLine($"- Hand: {player.HandCards.Count}");
        text.AppendLine($"- Trash: {player.TrashCards.Count}");
        text.AppendLine($"- Security: {player.SecurityCards.Count} ({faceUpSecurityCards.Count} face-up)");

        AppendPermanents(text, "Battle area", player.GetBattleAreaPermanents(), turnCount);
        AppendPermanents(text, "Breeding area", player.GetBreedingAreaPermanents(), turnCount);

        //The opponent's hand is hidden information
        if (player.isYou)
        {
            AppendCards(text, "Hand", player.HandCards);
        }

        AppendCards(text, "Trash", player.TrashCards);
        AppendCards(text, "Face-up security", faceUpSecurityCards);

        text.AppendLine();
        text.AppendLine("### Effects on this player");
        text.AppendLine();

        List<string> effectLines = new List<string>();

        AddEffectLines(effectLines, player.PermanentEffects, "permanent");
        AddEffectLines(effectLines, player.UntilEndBattleEffects, "until the end of the battle");
        AddEffectLines(effectLines, player.UntilEachTurnEndEffects, "until the end of this turn");
        AddEffectLines(effectLines, player.UntilOwnerTurnEndEffects, "until the end of this player's turn");
        AddEffectLines(effectLines, player.UntilOpponentTurnEndEffects, "until the end of the other player's turn");
        AddEffectLines(effectLines, player.UntilOwnerActivePhaseEffects, "until this player's active phase");
        AddEffectLines(effectLines, player.UntilSecurityCheckEndEffects, "until the end of the security check");
        AddEffectLines(effectLines, player.UntilCalculateFixedCostEffect, "until the next cost is calculated");

        AppendLinesOrNone(text, effectLines, "");

        text.AppendLine();
        text.AppendLine("### Effects used this game");
        text.AppendLine();

        List<EffectHistory.Entry> entries = EffectHistory.Entries.Filter(entry => entry.Player == player);

        if (entries.Count == 0)
        {
            text.AppendLine("- None");
        }

        int turn = -1;

        foreach (EffectHistory.Entry entry in entries)
        {
            if (entry.Turn != turn)
            {
                turn = entry.Turn;

                text.AppendLine($"- Turn {turn}");
            }

            text.AppendLine($"{Indent}- {CardLabel(entry.Card)}: \"{entry.EffectName}\"");
        }
    }

    static void AppendCards(StringBuilder text, string title, List<CardSource> cardSources)
    {
        text.AppendLine();
        text.AppendLine($"### {title} ({cardSources.Count})");
        text.AppendLine();

        if (cardSources.Count == 0)
        {
            text.AppendLine("- None");
        }

        foreach (CardSource cardSource in cardSources)
        {
            text.AppendLine($"- {CardLabel(cardSource)}");
        }
    }
    #endregion

    #region Permanent
    static void AppendPermanents(StringBuilder text, string title, List<Permanent> permanents, int turnCount)
    {
        permanents = permanents.Filter(permanent => permanent != null && permanent.TopCard != null);

        text.AppendLine();
        text.AppendLine($"### {title} ({permanents.Count})");
        text.AppendLine();

        if (permanents.Count == 0)
        {
            text.AppendLine("- None");
        }

        for (int i = 0; i < permanents.Count; i++)
        {
            AppendPermanent(text, i + 1, permanents[i], turnCount);
        }
    }

    static void AppendPermanent(StringBuilder text, int number, Permanent permanent, int turnCount)
    {
        List<string> details = new List<string>();

        if (permanent.TopCard.HasLevel)
        {
            details.Add($"Lv.{permanent.Level}");
        }

        details.Add(permanent.IsDigimon ? "Digimon" : permanent.IsTamer ? "Tamer" : permanent.IsOption ? "Option" : "Other");

        if (permanent.HasDP)
        {
            details.Add($"{permanent.DP} DP");
        }

        details.Add(permanent.IsSuspended ? "suspended" : "unsuspended");

        text.AppendLine($"{number}. **{CardLabel(permanent.TopCard)}** - {string.Join(", ", details)}");

        if (permanent.DigivolutionCards.Count >= 1)
        {
            text.AppendLine($"{Indent}- Digivolution cards (top to bottom): {CardLabels(permanent.DigivolutionCards)}");
        }

        if (permanent.LinkedCards.Count >= 1)
        {
            text.AppendLine($"{Indent}- Linked cards: {CardLabels(permanent.LinkedCards)}");
        }

        #region Effects used by the cards of this permanent
        List<string> usedLines = new List<string>();

        foreach (EffectHistory.Entry entry in EffectHistory.Entries)
        {
            if (permanent.cardSources.Contains(entry.Card))
            {
                usedLines.Add($"Turn {entry.Turn} - {CardLabel(entry.Card)}: \"{entry.EffectName}\"");
            }
        }

        text.AppendLine($"{Indent}- Effects used:");

        AppendLinesOrNone(text, usedLines, Indent + Indent);
        #endregion

        #region Uses the game counts against the per turn limits
        List<string> countedLines = new List<string>();

        foreach (CardSource cardSource in permanent.cardSources)
        {
            if (cardSource == null || cardSource.cEntity_EffectController == null)
            {
                continue;
            }

            foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.UseEffectsThisTurn)
            {
                if (cardEffect != null)
                {
                    countedLines.Add($"{CardLabel(cardSource)}: {EffectLabel(cardEffect)}");
                }
            }
        }

        text.AppendLine($"{Indent}- Uses counted this turn (turn {turnCount}, for once per turn limits):");

        AppendLinesOrNone(text, countedLines, Indent + Indent);
        #endregion

        #region Effects on this permanent
        List<string> effectLines = new List<string>();

        AddEffectLines(effectLines, permanent.PermanentEffects, "permanent");
        AddEffectLines(effectLines, permanent.UntilEndBattleEffects, "until the end of the battle");
        AddEffectLines(effectLines, permanent.UntilEndAttackEffects, "until the end of the attack");
        AddEffectLines(effectLines, permanent.UntilEachTurnEndEffects, "until the end of this turn");
        AddEffectLines(effectLines, permanent.UntilOwnerTurnEndEffects, "until the end of its owner's turn");
        AddEffectLines(effectLines, permanent.UntilOpponentTurnEndEffects, "until the end of the turn of its owner's opponent");
        AddEffectLines(effectLines, permanent.UntilOwnerTurnStartEffects, "until the start of its owner's turn");
        AddEffectLines(effectLines, permanent.UntilOwnerDrawPhaseEffects, "until its owner's draw phase");
        AddEffectLines(effectLines, permanent.UntilNextUntapEffects, "until it next unsuspends");

        text.AppendLine($"{Indent}- Effects on it:");

        AppendLinesOrNone(text, effectLines, Indent + Indent);
        #endregion
    }
    #endregion

    #region Effects
    //Lingering effects are stored as functions of the timing, so every timing has to be asked to find them
    static void AddEffectLines(List<string> lines, List<Func<EffectTiming, ICardEffect>> getCardEffects, string duration)
    {
        foreach (Func<EffectTiming, ICardEffect> getCardEffect in getCardEffects)
        {
            if (getCardEffect == null)
            {
                continue;
            }

            List<string> labels = new List<string>();

            foreach (EffectTiming timing in _effectTimings)
            {
                ICardEffect cardEffect = null;

                try
                {
                    cardEffect = getCardEffect(timing);
                }

                catch (Exception)
                {
                    continue;
                }

                if (cardEffect == null)
                {
                    continue;
                }

                string label = EffectLabel(cardEffect);

                if (labels.Contains(label))
                {
                    continue;
                }

                labels.Add(label);

                string source = cardEffect.EffectSourceCard != null ? CardLabel(cardEffect.EffectSourceCard) : "unknown source";

                lines.Add($"{label} - from {source}, {duration}");
            }
        }
    }

    static string EffectLabel(ICardEffect cardEffect)
    {
        string label = string.IsNullOrEmpty(cardEffect.EffectName) ? cardEffect.GetType().Name : $"\"{cardEffect.EffectName}\"";

        if (!string.IsNullOrEmpty(cardEffect.EffectDiscription))
        {
            label += $" ({cardEffect.EffectDiscription.Replace("\r", "").Replace("\n", " ")})";
        }

        return label;
    }
    #endregion

    #region Labels
    static void AppendLinesOrNone(StringBuilder text, List<string> lines, string indent)
    {
        if (lines.Count == 0)
        {
            text.AppendLine($"{indent}- None");
        }

        foreach (string line in lines)
        {
            text.AppendLine($"{indent}- {line}");
        }
    }

    static string PlayerLabel(Player player)
    {
        return $"{player.PlayerName} ({(player.isYou ? "you" : "opponent")})";
    }

    static string CardLabel(CardSource cardSource)
    {
        if (cardSource == null)
        {
            return "unknown card";
        }

        return $"{cardSource.BaseENGCardNameFromEntity} ({cardSource.CardID})";
    }

    static string CardLabels(List<CardSource> cardSources)
    {
        List<string> labels = new List<string>();

        foreach (CardSource cardSource in cardSources)
        {
            //Face-down cards under the opponent's Digimon are hidden information
            if (cardSource != null && cardSource.IsFaceDown && !cardSource.Owner.isYou)
            {
                labels.Add("face-down card");
            }

            else if (cardSource != null && cardSource.IsFaceDown)
            {
                labels.Add($"{CardLabel(cardSource)} [face-down]");
            }

            else
            {
                labels.Add(CardLabel(cardSource));
            }
        }

        return string.Join(", ", labels);
    }
    #endregion
}
