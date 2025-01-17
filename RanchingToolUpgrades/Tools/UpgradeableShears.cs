using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Netcode;
using BirbShared;

namespace RanchingToolUpgrades
{
    [XmlType("Mods_drbirbdev_upgradeableshears")]
    public class UpgradeableShears : Shears, ICustomIcon
    {
        public const int MaxUpgradeLevel = 4;

        public Rectangle IconSource()
        {
            Rectangle source = new(16, 0, 16, 16);
            source.Y += this.UpgradeLevel * source.Height;
            return source;
        }

        public UpgradeableShears() : base()
        {
            base.UpgradeLevel = 0;
            base.InitialParentTileIndex = -1;
            base.IndexOfMenuItemView = -1;
        }

        public UpgradeableShears(int upgradeLevel) : base()
        {
            base.UpgradeLevel = upgradeLevel;
            base.InitialParentTileIndex = -1;
            base.IndexOfMenuItemView = -1;
        }

        public static bool CanBeUpgraded()
        {
            Tool shears = Game1.player.getToolFromName("Shears");
            return shears is not null && shears.UpgradeLevel != MaxUpgradeLevel;
        }

        public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
        {
            spriteBatch.Draw(
                texture: ModEntry.Assets.Sprites,
                position: location + new Vector2(32f, 32f),
                sourceRectangle: this.IconSource(),
                color: color * transparency,
                rotation: 0f,
                origin: new Vector2(8, 8),
                scale: Game1.pixelZoom * scaleSize,
                effects: SpriteEffects.None,
                layerDepth: layerDepth);
        }

        public override bool canBeTrashed()
        {
            return false;
        }

        public override bool actionWhenPurchased()
        {
            if (this.UpgradeLevel > 0 && Game1.player.toolBeingUpgraded.Value == null)
            {
                Tool t = Game1.player.getToolFromName("Shears");
                Game1.player.removeItemFromInventory(t);
                if (t is not UpgradeableShears)
                {
                    t = new UpgradeableShears(upgradeLevel: 1);
                }
                else
                {
                    t.UpgradeLevel++;
                }
                Game1.player.toolBeingUpgraded.Value = t;
                Game1.player.daysLeftForToolUpgrade.Value = ModEntry.Config.ShearsUpgradeDays;
                Game1.playSound("parry");
                Game1.exitActiveMenu();
                Game1.drawDialogue(Game1.getCharacterFromName("Clint"), Game1.content.LoadString("Strings\\StringsFromCSFiles:Tool.cs.14317"));
                return true;
            }
            return base.actionWhenPurchased();
        }

        public static void AddToShopStock(Dictionary<ISalable, int[]> itemPriceAndStock, Farmer who)
        {
            if (who == Game1.player && CanBeUpgraded())
            {
                int quantity = 1;
                int upgradeLevel = who.getToolFromName("Shears").UpgradeLevel + 1;
                int upgradePrice = ModEntry.Instance.Helper.Reflection.GetMethod(
                    typeof(Utility), "priceForToolUpgradeLevel")
                    .Invoke<int>(upgradeLevel);
                upgradePrice = (int)(upgradePrice * ModEntry.Config.ShearsUpgradeCostMultiplier);
                int extraMaterialIndex = ModEntry.Instance.Helper.Reflection.GetMethod(
                    typeof(Utility), "indexOfExtraMaterialForToolUpgrade")
                    .Invoke<int>(upgradeLevel);
                itemPriceAndStock.Add(
                    new UpgradeableShears(upgradeLevel: upgradeLevel),
                    new int[] { upgradePrice, quantity, extraMaterialIndex, ModEntry.Config.ShearsUpgradeCostBars });
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("SMAPI.CommonErrors", "AvoidImplicitNetFieldCast:Netcode types shouldn't be implicitly converted", Justification = "<Pending>")]
        public override void DoFunction(GameLocation location, int x, int y, int power, Farmer who)
        {
            FarmAnimal animal = ModEntry.Instance.Helper.Reflection.GetField<FarmAnimal>((Shears)this, "animal").GetValue();

            if (animal != null && animal.currentProduce > 0 && animal.age >= animal.ageWhenMature && animal.toolUsedForHarvest.Equals(base.BaseName))
            {
                // do extra friendship effect
                int extraFriendship = ModEntry.Config.ExtraFriendshipBase * this.UpgradeLevel;
                animal.friendshipTowardFarmer.Value = Math.Min(1000, animal.friendshipTowardFarmer + extraFriendship);
                Log.Trace($"Applied extra friendship {extraFriendship}.  Total friendship: {animal.friendshipTowardFarmer.Value}");

                // do quality bump effect
                float higherQualityChance = ModEntry.Config.QualityBumpChanceBase * this.UpgradeLevel;
                if (higherQualityChance > Game1.random.NextDouble())
                {
                    switch (animal.produceQuality) {
                        case 0: animal.produceQuality.Set(1);
                            break;
                        case 1: animal.produceQuality.Set(2);
                            break;
                        case 2: animal.produceQuality.Set(4);
                            break;
                        default: break;
                    }
                    Log.Debug($"Quality Bump Chance {higherQualityChance}, succeeded.  New quality {animal.produceQuality.Value}");
                }
                else
                {
                    Log.Debug($"Quality Bump Chance {higherQualityChance} failed.");
                }

                // do extra produce effect
                int extraProduce = 0;
                for (int i = 0; i < this.UpgradeLevel; i++)
                {
                    if (ModEntry.Config.ExtraProduceChance > Game1.random.NextDouble())
                    {
                        extraProduce++;
                    }
                }
                Log.Debug($"Extra Produce Chance {ModEntry.Config.ExtraProduceChance} generated {extraProduce} additional produce from {this.UpgradeLevel} draws.");
                if (extraProduce > 0)
                {
                    who.addItemToInventory(new StardewValley.Object(Vector2.Zero, animal.currentProduce, null, false, true, false, false)
                    {
                        Quality = animal.produceQuality,
                        Stack = extraProduce
                    });
                }
            }

            base.DoFunction(location, x, y, power, who);
        }

    }
}
