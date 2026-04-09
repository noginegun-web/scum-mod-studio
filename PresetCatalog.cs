namespace ScumPakWizard;

internal static class PresetCatalog
{
    public static List<PresetDefinition> Load(string appRoot)
    {
        var definitions = new List<PresetDefinition>
        {
            new(
                "no-radiation",
                "No Radiation",
                "Отключает радиационные эффекты для безопасного PvE/PvP геймплея.",
                "no-radiation",
                PresetRootMode.ConZFilesRoot,
                ["Blueprints", "Characters", "BodyEffects"]),
            new(
                "extra-trader-items",
                "Extra Trader Items",
                "Расширяет ассортимент у торговцев (Economy/Table_TradeableDesc).",
                "extra-trader-items",
                PresetRootMode.ScumRoot,
                ["Economy", "Trader Tables"]),
            new(
                "more-quests-in-book",
                "More Quests In Book",
                "Добавляет/открывает больше квестов в журнале.",
                "more-quests-in-book",
                PresetRootMode.ScumRoot,
                ["Quests", "QuestBook"]),
            new(
                "advanced-starter-items",
                "Advanced Starter Items",
                "Расширяет стартовую экипировку персонажа.",
                "advanced-starter-items",
                PresetRootMode.ScumRoot,
                ["SpawnEquipment", "Starter Loadout"]),
            new(
                "crafting-overhaul",
                "Crafting Overhaul",
                "Изменяет рецепты, ингредиенты, крафт-UI и связанные таблицы.",
                "crafting-overhaul",
                PresetRootMode.ScumRoot,
                ["Crafting Recipes", "Ingredients", "UI", "Item Tables"]),
            new(
                "more-armed-npc-hordes",
                "More Armed NPC Hordes",
                "Увеличивает интенсивность NPC/энкаунтеров и связанные таблицы зон.",
                "more-armed-npc-hordes",
                PresetRootMode.ScumRoot,
                ["Encounters", "NPCs", "Spawn Curves", "Zones"])
        };

        var available = new List<PresetDefinition>();
        foreach (var preset in definitions)
        {
            var path = Path.Combine(appRoot, "presets", preset.FolderName);
            if (Directory.Exists(path))
            {
                available.Add(preset);
            }
        }

        return available;
    }
}
