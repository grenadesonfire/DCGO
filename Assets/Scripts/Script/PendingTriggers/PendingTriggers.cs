using System.Collections.Generic;

public class PendingTriggerGroup
{
    public PendingTriggerGroup(string label, List<SkillInfo> skillInfos)
    {
        Label = label;
        SkillInfos = skillInfos;
    }

    public string Label { get; private set; }

    //Your effects first, the opponent's effects at the end
    public List<SkillInfo> SkillInfos { get; private set; }
}

//Read-only view of every effect that has triggered and is still waiting to be resolved
public static class PendingTriggers
{
    #region Get the batches of pending effects, in the order they will be resolved
    public static List<PendingTriggerGroup> GetGroups()
    {
        List<PendingTriggerGroup> groups = new List<PendingTriggerGroup>();

        if (GManager.instance == null) return groups;

        //Cut-in processing interrupts the main processing, so it comes first
        List<AutoProcessing> autoProcessings = new List<AutoProcessing>()
        {
            GManager.instance.autoProcessing_CutIn,
            GManager.instance.autoProcessing,
        };

        List<List<SkillInfo>> batches = new List<List<SkillInfo>>();

        #region Effects that have triggered but have not entered resolution timing yet
        foreach (AutoProcessing autoProcessing in autoProcessings)
        {
            if (autoProcessing == null) continue;

            batches.Add(new List<SkillInfo>(autoProcessing.StackedSkillInfos));
        }
        #endregion

        #region Batches being resolved, from the most recently triggered one
        foreach (AutoProcessing autoProcessing in autoProcessings)
        {
            if (autoProcessing == null) continue;

            for (int i = autoProcessing.multipleSkills.Count - 1; i >= 0; i--)
            {
                MultipleSkills multipleSkills = autoProcessing.multipleSkills[i];

                if (multipleSkills == null) continue;
                if (!multipleSkills.IsUsing) continue;

                List<SkillInfo> batch = new List<SkillInfo>(multipleSkills.StackedSkillInfos);
                batch.AddRange(multipleSkills.WaitingSkillInfos);

                batches.Add(batch);
            }
        }
        #endregion

        foreach (List<SkillInfo> batch in batches)
        {
            List<SkillInfo> skillInfos = SortOpponentLast(batch.Filter(CanShow));

            if (skillInfos.Count == 0) continue;

            string label = groups.Count == 0 ? "Batch 1 - resolves first" : $"Batch {groups.Count + 1}";

            groups.Add(new PendingTriggerGroup(label, skillInfos));
        }

        return groups;
    }
    #endregion

    #region Whether the effect belongs to the opponent
    public static bool IsOpponentEffect(SkillInfo skillInfo)
    {
        return !skillInfo.CardEffect.EffectSourceCard.Owner.isYou;
    }
    #endregion

    #region Whether the effect would currently be activated when its turn comes
    public static bool CanActivate(SkillInfo skillInfo)
    {
        try
        {
            return skillInfo.CardEffect.CanActivate(skillInfo.Hashtable);
        }

        catch (System.Exception)
        {
            return true;
        }
    }
    #endregion

    static bool CanShow(SkillInfo skillInfo)
    {
        if (skillInfo == null) return false;
        if (skillInfo.CardEffect == null) return false;
        if (skillInfo.CardEffect.EffectSourceCard == null) return false;

        return !IsHiddenFromYou(skillInfo.CardEffect.EffectSourceCard);
    }

    //Effects of the opponent's cards you are not allowed to see (e.g. Blast Digivolve from hand) must not be revealed
    static bool IsHiddenFromYou(CardSource card)
    {
        if (card.Owner == null) return true;
        if (card.Owner.isYou) return false;

        if (card.Owner.HandCards.Contains(card)) return true;
        if (card.Owner.LibraryCards.Contains(card)) return true;
        if (card.Owner.SecurityCards.Contains(card) && card.IsFlipped) return true;

        return false;
    }

    static List<SkillInfo> SortOpponentLast(List<SkillInfo> skillInfos)
    {
        List<SkillInfo> sorted = skillInfos.Filter(skillInfo => !IsOpponentEffect(skillInfo));
        sorted.AddRange(skillInfos.Filter(IsOpponentEffect));

        return sorted;
    }
}
