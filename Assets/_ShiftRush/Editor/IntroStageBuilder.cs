using System.Collections.Generic;
using ShiftRush.Comic;
using ShiftRush.Core;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ShiftRush.EditorTools
{
    /// <summary>
    /// SHIFT RUSH → Comic → Build Intro Sets: stages the origin comic in the open ComicStage scene. Every panel is a
    /// camera named intro_NN with its lettering, looking at a small set built from primitives, lights and the cast
    /// (ComicActor bodies dressed from the wardrobe catalog). Sets sit behind the showroom, apart from each other.
    /// Running it again rebuilds "Intro Sets" from scratch, so tune the numbers here, not in the scene.
    /// Then SHIFT RUSH → Comic → Capture Intro Panels turns the cameras into the pages the game shows.
    /// </summary>
    public static class IntroStageBuilder
    {
        const string RootName = "Intro Sets";
        const string MatFolder = "Assets/_ShiftRush/Art/Comic/Materials/";

        static Transform root;

        [MenuItem("SHIFT RUSH/Comic/Build Intro Sets")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "ComicStage") { Debug.LogError("[COMIC] Open ComicStage first. The intro sets only belong in that scene (active: " + scene.name + ")."); return; }
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            root = new GameObject(RootName).transform;
            System.IO.Directory.CreateDirectory(MatFolder);

            Garage(new Vector3(0, 0, 200));
            Office(new Vector3(60, 0, 200));
            Montage(new Vector3(120, 0, 200));
            Bedroom(new Vector3(180, 0, 200));
            City(new Vector3(-160, 0, -150));
            NewsDesk(new Vector3(240, 0, 200));
            PoliceHQ(new Vector3(300, 0, 200));
            Alpine(new Vector3(2000, 0, 0));

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            foreach (var l in Object.FindObjectsByType<ComicLettering>(FindObjectsSortMode.None)) l.Refresh();
            Debug.Log("[COMIC] Intro sets built");
        }

        // ---------------- outfits ----------------

        static readonly string[] Dee = { "Hair=m_c3cc1_hair_10_2", "Beard=m_c3cc1_beard_01_1", "Hat=m_c3cc1_hat_05_2", "Torso=m_c3cc1_torso_10_1", "Pants=m_c3cc1_pants_15_1", "Shoes=m_c3cc1_shoes_02_1" };
        static readonly string[] Kojo = { "Hair=m_loose_hair_08_1", "Beard=m_loose_beard_04_1", "Torso=m_c3cc1_torso_13_2", "Pants=m_c3cc1_pants_18_1", "Shoes=m_c3cc1_shoes_03_1", "Gloves=m_loose_hand_06_1" };
        static readonly string[] Rook = { "Hair=f_c3cc1_hair_18_2", "Glasses=f_c3cc1_glasses_06_1", "Earrings=f_c3cc1_earring_02_1", "Torso=f_c3cc1_torso_07_1", "Pants=f_c3cc1_pants_07_1", "Shoes=f_c3cc1_shoes_04_1" };
        static readonly string[] Slim = { "Hat=m_c3cc1_hat_14_1", "Torso=m_c3cc1_torso_06_1", "Pants=m_c3cc1_pants_13_1", "Shoes=m_c3cc1_shoes_02_3", "Watch=m_c3cc1_watches_02_1", "Hair=m_c3cc1_hair_01_1" };
        static readonly string[] Hex = { "Hair=f_c3cc1_hair_06_2", "Torso=f_c3cc1_torso_21_2", "Pants=f_c3cc1_pants_12_1", "Shoes=f_c3cc1_shoes_05_1", "Headphones=f_c3cc1_headphones_06_1" };
        static readonly string[] Vex = { "Hair=f_c3cc1_hair_15_1", "Torso=f_c3cc1_torso_07_3", "Pants=f_c3cc1_pants_04_2", "Shoes=f_c3cc1_shoes_03_2", "Glasses=f_c3cc1_glasses_01_2" };
        static readonly string[] Kaito = { "Hair=m_c3cc1_hair_02_2", "Beard=m_c3cc1_beard_02_1", "Torso=m_c3cc1_torso_22_2", "Pants=m_c3cc1_pants_12_2", "Shoes=m_c3cc1_shoes_02_2" };
        static readonly string[] HostA = { "Mask=m_loose_mask_04_1", "Hair=m_c3cc1_hair_03_1", "Torso=m_c3cc1_torso_09_1", "Pants=m_c3cc1_pants_13_2", "Shoes=m_c3cc1_shoes_02_1" };
        static readonly string[] HostB = { "Mask=f_loose_mask_06_1", "Hair=f_c3cc1_hair_19_1", "Torso=f_c3cc1_torso_15_1", "Pants=f_c3cc1_pants_02_1", "Shoes=f_c3cc1_shoes_04_2" };
        static readonly string[] Chief = { "Hair=m_c3cc1_hair_01_3", "Beard=m_c3cc1_beard_07_1", "Hat=m_c3cc1_hat_05_3", "Torso=m_c3cc1_torso_22_3", "Pants=m_c3cc1_pants_13_3", "Shoes=m_c3cc1_shoes_03_3" };
        static readonly string[] OfficerA = { "Hair=f_c3cc1_hair_06_3", "Torso=f_c3cc1_torso_19_3", "Pants=f_c3cc1_pants_02_3", "Shoes=f_c3cc1_shoes_03_3" };
        static readonly string[] OfficerB = { "Hair=m_c3cc1_hair_02_3", "Hat=m_c3cc1_hat_05_3", "Torso=m_c3cc1_torso_07_3", "Pants=m_c3cc1_pants_13_3", "Shoes=m_c3cc1_shoes_03_3" };
        static readonly string[] CrowdA = { "Hair=m_c3cc1_hair_03_2", "Torso=m_c3cc1_torso_05_2", "Pants=m_c3cc1_pants_12_3", "Shoes=m_c3cc1_shoes_05_1" };
        static readonly string[] CrowdB = { "Hair=f_c3cc1_hair_15_3", "Torso=f_c3cc1_torso_11_2", "Pants=f_c3cc1_pants_20_1", "Shoes=f_c3cc1_shoes_05_2" };

        // ---------------- sets ----------------

        static void Garage(Vector3 o)
        {
            var set = Set("01-02 Garage", o);
            Room(set, new Vector3(14, 6, 12), Mat("Concrete", new Color(0.16f, 0.16f, 0.19f)), Mat("Garage Wall", new Color(0.12f, 0.11f, 0.15f)));
            Box(set, "Neon Strip", new Vector3(0, 3.6f, -5.9f), new Vector3(9, 0.12f, 0.1f), Glow("Neon Magenta", Magenta, 4f));
            Box(set, "Neon Strip Low", new Vector3(-5.5f, 1.2f, -5.9f), new Vector3(2.5f, 0.08f, 0.1f), Glow("Neon Cyan", Cyan, 4f));
            Box(set, "Workbench", new Vector3(4.6f, 0.45f, -4.8f), new Vector3(3f, 0.9f, 1.1f), Mat("Steel", new Color(0.3f, 0.32f, 0.36f)));
            Box(set, "Tyre Stack", new Vector3(-5.2f, 0.6f, -4.6f), new Vector3(1f, 1.2f, 1f), Mat("Rubber", new Color(0.05f, 0.05f, 0.06f)));
            var car = new GameObject("Car").transform; car.SetParent(set, false);
            car.localPosition = new Vector3(0, 0, -1.5f); car.localRotation = Quaternion.Euler(0, 70, 0);
            car.gameObject.AddComponent<ComicCar>().carId = "blackout";
            Light(set, "Work Lamp", LightType.Spot, new Vector3(0, 5.5f, 0), new Vector3(90, 0, 0), new Color(1f, 0.9f, 0.75f), 60f, 14f, 80f);
            Light(set, "Magenta Bounce", LightType.Point, new Vector3(0, 2.5f, -4.5f), Vector3.zero, Magenta, 6f, 9f);
            Light(set, "Cyan Fill", LightType.Point, new Vector3(-5, 1.5f, 3), Vector3.zero, Cyan, 3f, 10f);
            Light(set, "Front Key", LightType.Spot, new Vector3(0.5f, 3.2f, 5.2f), new Vector3(25, 180, 0), new Color(1f, 0.85f, 0.8f), 14f, 12f, 70f);

            var dee = Actor(set, "Dee", new Vector3(1.45f, 0, 1.6f), -30, DriverBody.BodyA, Dee, "MCU_am_Stand_Conv_Talk_05_Generic", 0.35f);
            dee.headTurn = 0.2f;
            var kojo = Actor(set, "Kojo", new Vector3(-1.35f, 0, 1.7f), 25, DriverBody.BodyA, Kojo, "CA_Idle04_ArmsFolded_Loop", 0.3f);
            kojo.headTurn = -0.25f;
            var rook = Actor(set, "Rook", new Vector3(0.1f, 0, 2.2f), 5, DriverBody.BodyB, Rook, "CA_Idle01_Breathing", 0.3f);
            rook.rightReach.on = true; rook.rightReach.fromHead = new Vector3(0.02f, -0.38f, 0.38f); rook.rightReach.elbowHint = new Vector3(0.7f, -0.7f, 0f);
            rook.rightHand.shape = ComicActor.Shape.Grip;
            rook.headNod = -0.45f;
            var part = Box(set, "Rig Part", Vector3.zero, new Vector3(0.07f, 0.07f, 0.14f), Glow("Rig Glow", Cyan, 1.6f));
            Object.DestroyImmediate(part.GetComponent<Collider>());
            Light(part, "Rig Light", LightType.Point, Vector3.zero, Vector3.zero, Cyan, 3f, 2.5f);
            rook.rightProp = part; rook.rightPropOffset = new Vector3(0f, 0.09f, 0.04f);

            var p1 = Panel(set, "intro_01", new Vector3(0.5f, 1.6f, 5.4f), new Vector3(0, 1.2f, 0.8f), 44);
            p1.captions.Add(Cap("BEFORE IT HAD A NAME.", "A GARAGE ON THE EDGE OF THE CITY.", 0.025f, 0.04f, 640));
            p1.bubbles.Add(Say("DEE", "Same tracks. Same cars. Same winners. Nobody's watching anymore.", 0.21f, 0.3f, 520, 220, Head(dee)));
            p1.bubbles.Add(Say("KOJO", "Then give them something they've never seen.", 0.8f, 0.2f, 460, 190, Head(kojo)));

            var p2 = Panel(set, "intro_02", new Vector3(-0.7f, 1.4f, 4.1f), new Vector3(0.1f, 1.3f, 2.2f), 32);
            p2.bubbles.Add(Say("ROOK", "Cars that do what cars can't.", 0.76f, 0.22f, 440, 190, Head(rook)));
            p2.sfx.Add(Fx("VZZT", 0.2f, 0.72f, -10f, 150f, true));
        }

        static void Office(Vector3 o)
        {
            var set = Set("03-04 Slim's Office", o);
            Room(set, new Vector3(10, 4.5f, 10), Mat("Office Floor", new Color(0.09f, 0.06f, 0.07f)), Mat("Office Wall", new Color(0.07f, 0.05f, 0.08f)));
            // The window behind Slim is the only bright thing in the room, so he's a shape against it.
            Box(set, "City Glow Window", new Vector3(0, 2.2f, -4.85f), new Vector3(7f, 3.4f, 0.05f), Glow("Window Glow", new Color(0.9f, 0.12f, 0.35f), 0.9f));
            for (int i = -3; i <= 3; i++) Box(set, "Blind " + i, new Vector3(i, 2.2f, -4.8f), new Vector3(0.08f, 3.4f, 0.06f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            Box(set, "Desk", new Vector3(0, 0.38f, -2.6f), new Vector3(2.8f, 0.76f, 1.1f), Mat("Desk Wood", new Color(0.18f, 0.08f, 0.06f)));
            Box(set, "Desk Top", new Vector3(0, 0.78f, -2.6f), new Vector3(2.9f, 0.04f, 1.2f), Mat("Black Gloss", new Color(0.03f, 0.03f, 0.04f), 0.9f));
            var phone = Box(set, "Black Phone", new Vector3(0.26f, 0.81f, -2.68f), new Vector3(0.08f, 0.012f, 0.16f), Mat("Phone", new Color(0.01f, 0.01f, 0.01f), 0.95f));
            phone.localRotation = Quaternion.Euler(0, 20, 0);
            Box(set, "Ashtray", new Vector3(-0.8f, 0.8f, -2.4f), new Vector3(0.18f, 0.04f, 0.18f), Mat("Steel", new Color(0.3f, 0.32f, 0.36f)));
            // Straight down onto the desk: his hands and the phone catch the light, his face never does.
            Light(set, "Desk Lamp", LightType.Spot, new Vector3(0, 1.75f, -2.55f), new Vector3(90, 0, 0), new Color(1f, 0.75f, 0.45f), 6f, 3f, 55f);
            Light(set, "Window Spill", LightType.Point, new Vector3(0, 2.4f, -4.2f), Vector3.zero, new Color(1f, 0.3f, 0.5f), 6f, 7f);

            // Sunk behind the desk so he reads as seated. Hands on the desk, the right one on the phone.
            var slim = Actor(set, "Slim", new Vector3(0, -0.15f, -3.22f), 0, DriverBody.BodyA, Slim, "CA_Idle01_Breathing", 0.3f);
            slim.spineLean = 0.55f; slim.headNod = -0.3f;
            var leftSpot = new GameObject("Slim Left Hand Spot").transform; leftSpot.SetParent(set, false); leftSpot.localPosition = new Vector3(-0.38f, 0.86f, -2.95f);
            var rightSpot = new GameObject("Slim Right Hand Spot").transform; rightSpot.SetParent(set, false); rightSpot.localPosition = new Vector3(0.24f, 0.86f, -2.8f);
            slim.leftReach.on = true; slim.leftReach.target = leftSpot; slim.leftReach.elbowHint = new Vector3(-1f, -0.3f, 0f);
            slim.rightReach.on = true; slim.rightReach.target = rightSpot; slim.rightReach.elbowHint = new Vector3(1f, -0.3f, 0f);
            slim.rightHand.shape = ComicActor.Shape.Open; slim.leftHand.shape = ComicActor.Shape.Relaxed;
            var dee = Actor(set, "Dee", new Vector3(1.4f, 0, 0.2f), 195, DriverBody.BodyA, Dee, "MCU_am_Stand_Conv_Talk_05_Generic", 0.6f);
            Actor(set, "Kojo", new Vector3(-0.95f, 0, 0.3f), 175, DriverBody.BodyA, Kojo, "CA_Idle04_ArmsFolded_Loop", 0.3f);
            var rook = Actor(set, "Rook", new Vector3(0.8f, 0, 0.9f), 185, DriverBody.BodyB, Rook, "CA_Idle01_Breathing", 0.5f);
            var smoke = PrefabByName("FX_Smoke_01");
            if (smoke != null) { var s = (GameObject)PrefabUtility.InstantiatePrefab(smoke, set); s.transform.localPosition = new Vector3(-0.8f, 0.85f, -2.4f); s.transform.localScale = Vector3.one * 0.3f; }

            var p3 = Panel(set, "intro_03", new Vector3(0.35f, 1.75f, 3.4f), new Vector3(0, 1.25f, -3.2f), 40);
            p3.captions.Add(Cap("THEY NEEDED MONEY.", "THEY KNEW WHO HAD IT.", 0.025f, 0.04f, 520));
            p3.bubbles.Add(Say("DEE", "Magic on wheels. Streamed live. The fans pick the heroes.", 0.2f, 0.27f, 500, 210, Head(dee)));
            p3.bubbles.Add(Say("SLIM", "And when the police come?", 0.74f, 0.17f, 420, 170, Head(slim)));

            var p4 = Panel(set, "intro_04", new Vector3(0.55f, 0.98f, -1.35f), new Vector3(0.1f, 0.8f, -2.4f), 38);
            p4.bubbles.Add(Say("ROOK", "They won't find us.", 0.2f, 0.2f, 380, 160, Head(rook), new Vector2(0.0f, 0.55f)));
            p4.bubbles.Add(Say("SLIM", "Then you're on my channel. Don't make me regret it.", 0.7f, 0.2f, 520, 210, Head(slim)));
        }

        static void Montage(Vector3 o)
        {
            // Three tall slices side by side in one panel: each racer's face as the call comes in.
            var set = Set("05 Phone Montage", o);
            var names = new[] { "Hex", "Vex", "Kaito" };
            var looks = new[] { Hex, Vex, Kaito };
            var bodies = new[] { DriverBody.BodyB, DriverBody.BodyB, DriverBody.BodyA };
            var tints = new[] { Magenta, Cyan, new Color(0.62f, 0.3f, 1f) };
            var lines = new[] { "Who's this?", "Talk.", "Say that again." };
            var slices = new[] { "intro_05a", "intro_05b", "intro_05c" };
            for (int i = 0; i < 3; i++)
            {
                var booth = new GameObject(names[i] + " Booth").transform; booth.SetParent(set, false);
                booth.localPosition = new Vector3(i * 6f, 0, 0);
                Box(booth, "Wall", new Vector3(0, 1.8f, -1.2f), new Vector3(3f, 3.6f, 0.1f), Glow("Wall " + names[i], tints[i], 0.9f));
                Box(booth, "Floor", new Vector3(0, -0.05f, 0), new Vector3(3f, 0.1f, 3f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
                var a = Actor(booth, names[i], Vector3.zero, i == 1 ? 8f : -10f, bodies[i], looks[i], "CA_Idle01_Breathing", 0.2f + i * 0.2f);
                float headY = bodies[i] == DriverBody.BodyB ? 1.55f : 1.65f;
                if (i == 1)
                {
                    // Vex reads the screen before she answers.
                    var phone = Box(booth, "Phone", Vector3.zero, new Vector3(0.075f, 0.15f, 0.012f), Glow("Phone Screen", Cyan, 1.5f));
                    Object.DestroyImmediate(phone.GetComponent<Collider>());
                    a.rightReach.on = true; a.rightReach.fromHead = new Vector3(0.05f, -0.2f, 0.3f); a.rightReach.elbowHint = new Vector3(0.6f, -0.8f, 0f);
                    a.rightHand.shape = ComicActor.Shape.Grip;
                    a.rightProp = phone; a.rightPropOffset = new Vector3(0f, 0.08f, 0.03f); a.rightPropEuler = new Vector3(-60, 0, 0);
                    a.headNod = -0.35f;
                    Light(booth, "Screen Glow", LightType.Point, new Vector3(0.05f, headY - 0.1f, 0.35f), Vector3.zero, Cyan, 1.5f, 1.2f);
                }
                else
                {
                    PhoneToEar(booth, a, i == 2);
                    a.headTilt = i == 2 ? -0.2f : 0.2f;
                    a.headTurn = 0.45f;
                }
                Light(booth, "Face Key", LightType.Spot, new Vector3(0.6f, headY + 0.6f, 1.6f), new Vector3(20, 200, 0), new Color(1f, 0.92f, 0.85f), 5f, 4f, 40f);
                Light(booth, "Rim", LightType.Point, new Vector3(-0.5f, headY + 0.3f, -0.8f), Vector3.zero, tints[i], 3f, 2.5f);
                var l = Panel(booth, slices[i], new Vector3(0.05f, headY - 0.05f, 1.45f), new Vector3(0, headY + 0.02f, 0), 30, new Vector2Int(586, 1100));
                if (i == 0) l.captions.Add(Cap("THE INVITES", "WENT OUT BY PHONE.", 0.05f, 0.03f, 440));
                l.captions.Add(Cap(names[i].ToUpper(), "", 0.07f, 0.9f, 170));
                l.bubbles.Add(Say(names[i].ToUpper(), lines[i], 0.5f, i == 0 ? 0.27f : 0.12f, 400, 150, Head(a), default(Vector2)));
                if (i == 2) l.sfx.Add(Fx("BZZT", 0.26f, 0.62f, -12f, 90f, true));
            }
        }

        static void Bedroom(Vector3 o)
        {
            var set = Set("06 Driver's Room", o);
            Room(set, new Vector3(8, 3.4f, 8), Mat("Room Floor", new Color(0.14f, 0.12f, 0.12f)), Mat("Room Wall", new Color(0.13f, 0.12f, 0.2f)));
            Box(set, "Window", new Vector3(-1.6f, 1.9f, -3.85f), new Vector3(2.2f, 1.6f, 0.05f), Glow("Night Window", new Color(0.25f, 0.35f, 0.9f), 1.6f));
            Box(set, "Poster CH13", new Vector3(1.6f, 2f, -3.9f), new Vector3(1f, 1.4f, 0.03f), Glow("Poster Magenta", Magenta, 1.2f));
            Text3D(set, "Poster Text", "CH\n13", new Vector3(1.6f, 2f, -3.86f), 0.55f, Color.white);
            Box(set, "Bed", new Vector3(2.2f, 0.25f, -2f), new Vector3(1.6f, 0.5f, 2.2f), Mat("Sheets", new Color(0.2f, 0.22f, 0.35f)));
            Box(set, "Lamp Stand", new Vector3(-2.8f, 0.5f, -3.2f), new Vector3(0.05f, 1f, 0.05f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            Primitive(PrimitiveType.Cylinder, set, "Lamp Shade", new Vector3(-2.8f, 1.05f, -3.2f), new Vector3(0.32f, 0.12f, 0.32f), Glow("Lamp Warm", new Color(1f, 0.6f, 0.3f), 0.9f));
            Light(set, "Lamp Light", LightType.Point, new Vector3(-2.8f, 1.4f, -3f), Vector3.zero, new Color(1f, 0.6f, 0.35f), 5f, 6f);
            Light(set, "Window Light", LightType.Spot, new Vector3(-1.6f, 2.2f, -3.2f), new Vector3(20, 160, 0), new Color(0.4f, 0.5f, 1f), 8f, 8f, 60f);
            Light(set, "Driver Key", LightType.Spot, new Vector3(1.2f, 2.6f, 1.2f), new Vector3(35, 210, 0), new Color(1f, 0.85f, 0.75f), 9f, 6f, 45f);
            var drv = Actor(set, "Driver (player's look)", new Vector3(0, 0, -1.4f), 20, DriverBody.BodyA, null, "CA_Idle09_CoolPose1", 0.05f);
            drv.outfit = ComicActor.Outfit.PlayersSaved;
            PhoneToEar(set, drv, false);
            var p6 = Panel(set, "intro_06", new Vector3(0.9f, 1.6f, 1.7f), new Vector3(-0.1f, 1.45f, -1.4f), 40);
            p6.bubbles.Add(Say("DEE  //  ON THE PHONE", "You're not top ten. Not even close. But I know what you can do.", 0.25f, 0.3f, 620, 250, null, new Vector2(0f, 0.08f), true));
            p6.bubbles.Add(Say("YOU", "When?", 0.78f, 0.24f, 260, 130, Head(drv)));
        }

        static void City(Vector3 o)
        {
            var set = Set("07 City Sky", o);
            var balloon = Balloon(set, new Vector3(5, 26, -38));
            balloon.localRotation = Quaternion.Euler(0, -15, 0);
            var aimFrom = new Vector3(0, 1.6f, 6);
            var a = Actor(set, "Crowd 1", new Vector3(-1.2f, 0, 2.4f), 200, DriverBody.BodyA, CrowdA, "CA_Idle01_Breathing", 0.3f);
            Pointing(a);
            var b = Actor(set, "Crowd 2", new Vector3(0.9f, 0, 2.9f), 160, DriverBody.BodyB, CrowdB, "CA_Idle18_Surprised2", 0.5f);
            b.headNod = 0.6f;
            var c = Actor(set, "Crowd 3", new Vector3(2.2f, 0, 1.6f), 190, DriverBody.BodyA, Kaito, "CA_Idle01_Breathing", 0.6f);
            Pointing(c);
            Light(set, "Street Light", LightType.Spot, new Vector3(0, 6, 5), new Vector3(60, 180, 0), new Color(1f, 0.8f, 0.6f), 12f, 12f, 70f);
            Light(set, "Screen Glow", LightType.Point, new Vector3(6, 26, -55), Vector3.zero, Magenta, 30f, 80f);
            var p7 = Panel(set, "intro_07", aimFrom, new Vector3(3.5f, 17, -38), 54);
            p7.captions.Add(Cap("NEXT MORNING, EVERY STREET IN THE CITY.", "", 0.025f, 0.04f, 700));
            p7.sfx.Add(Fx("WHOA!", 0.82f, 0.82f, 8f, 120f, false));
        }

        static void NewsDesk(Vector3 o)
        {
            var set = Set("08 Pirate News", o);
            Room(set, new Vector3(10, 4.5f, 9), Mat("Studio Floor", new Color(0.05f, 0.05f, 0.08f)), Mat("Studio Wall", new Color(0.04f, 0.04f, 0.07f)));
            Box(set, "Big Screen", new Vector3(0, 2.4f, -3.8f), new Vector3(6f, 2.6f, 0.08f), Glow("Studio Screen", new Color(0.05f, 0.5f, 0.6f), 0.55f));
            Text3D(set, "Screen Text", "CHANNEL 13\n<size=60%>BREAKING // LIVE</size>", new Vector3(0, 2.95f, -3.74f), 0.55f, Color.white);
            Box(set, "News Desk", new Vector3(0, 0.5f, -1.2f), new Vector3(3.6f, 1f, 0.9f), Mat("Desk Black", new Color(0.03f, 0.03f, 0.04f), 0.8f));
            Box(set, "Desk Stripe", new Vector3(0, 0.75f, -0.74f), new Vector3(3.6f, 0.08f, 0.02f), Glow("Neon Magenta", Magenta, 4f));
            var a = Actor(set, "Host A", new Vector3(-0.85f, 0, -1.9f), 12, DriverBody.BodyA, HostA, "MCU_am_Stand_Conv_Talk_05_Generic", 0.5f);
            var b = Actor(set, "Host B", new Vector3(0.85f, 0, -1.9f), -12, DriverBody.BodyB, HostB, "CA_Idle09_CoolPose1", 0.05f);
            Light(set, "Key", LightType.Spot, new Vector3(0, 3.2f, 2.5f), new Vector3(30, 180, 0), Color.white, 14f, 9f, 55f);
            Light(set, "Rim Magenta", LightType.Point, new Vector3(-2.5f, 2.5f, -3), Vector3.zero, Magenta, 6f, 6f);
            var p8 = Panel(set, "intro_08", new Vector3(0, 1.55f, 3.6f), new Vector3(0, 1.45f, -2f), 40);
            p8.bubbles.Add(Say("MASK ONE", "Breaking. The app nobody can delete.", 0.17f, 0.34f, 420, 190, Head(a)));
            p8.bubbles.Add(Say("MASK TWO", "Over two hundred copies. Every one of them live.", 0.83f, 0.34f, 440, 200, Head(b)));
        }

        static void PoliceHQ(Vector3 o)
        {
            var set = Set("09-11 Police HQ", o);
            Room(set, new Vector3(12, 4f, 10), Mat("HQ Floor", new Color(0.12f, 0.13f, 0.15f)), Mat("HQ Wall", new Color(0.18f, 0.2f, 0.24f)));
            // Wall of monitors, every one showing a copy of the app.
            for (int x = 0; x < 6; x++)
                for (int y = 0; y < 3; y++)
                {
                    var p = new Vector3(-3.5f + x * 1.4f, 1.3f + y * 0.85f, -4.85f);
                    Box(set, "Monitor " + x + y, p, new Vector3(1.3f, 0.75f, 0.06f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
                    Box(set, "Screen " + x + y, p + new Vector3(0, 0, 0.035f), new Vector3(1.2f, 0.65f, 0.01f), Glow("Screen Red", new Color(0.9f, 0.1f, 0.2f), 1.2f));
                    Text3D(set, "App " + x + y, "CH13", p + new Vector3(0, 0, 0.05f), 0.28f, Color.white);
                }
            Box(set, "Console", new Vector3(0, 0.45f, -3.3f), new Vector3(6f, 0.9f, 0.8f), Mat("Steel", new Color(0.3f, 0.32f, 0.36f)));
            // Side wall window with the balloon far outside.
            Box(set, "Window", new Vector3(5.95f, 2f, 0f), new Vector3(0.05f, 2.6f, 4.5f), Glow("Day Window", new Color(0.35f, 0.5f, 0.85f), 0.7f));
            // The balloon, far off through the window (a small model just inside the glass reads as distance).
            var far = Balloon(set, new Vector3(5.85f, 2.6f, -0.6f));
            far.localScale = Vector3.one * 0.045f; far.localRotation = Quaternion.Euler(0, -90, 0);
            Light(set, "Monitor Wash", LightType.Point, new Vector3(0, 2f, -3.5f), Vector3.zero, new Color(1f, 0.2f, 0.3f), 8f, 9f);
            Light(set, "Siren Red", LightType.Point, new Vector3(-3, 3.5f, 2), Vector3.zero, new Color(1f, 0.1f, 0.15f), 10f, 9f);
            Light(set, "Siren Blue", LightType.Point, new Vector3(3, 3.5f, 2), Vector3.zero, new Color(0.15f, 0.35f, 1f), 10f, 9f);
            Light(set, "Window Light", LightType.Spot, new Vector3(5.7f, 2.4f, 0), new Vector3(15, 270, 0), new Color(0.7f, 0.85f, 1f), 10f, 10f, 70f);

            var chief = Actor(set, "Chief", new Vector3(-0.6f, 0, -1.4f), 140, DriverBody.BodyA, Chief, "CA_Idle21_Angry", 0.4f);
            var off = Actor(set, "Officer", new Vector3(1.2f, 0, -2.6f), -60, DriverBody.BodyB, OfficerA, "CA_Idle06_Phew", 0.4f);
            off.headTurn = 0.3f;
            var p9 = Panel(set, "intro_09", new Vector3(-1.4f, 1.7f, 2.4f), new Vector3(0.2f, 1.4f, -2.4f), 46);
            p9.bubbles.Add(Say("CHIEF", "Who built it? Trace it.", 0.18f, 0.3f, 380, 170, Head(chief)));
            p9.bubbles.Add(Say("OFFICER", "Sir, there are over two hundred. We can't tell which one is real.", 0.76f, 0.24f, 560, 230, Head(off)));

            // Panel 10: the chief at the side window, a second officer behind him.
            var chief2 = Actor(set, "Chief at Window", new Vector3(4.6f, 0, 0.6f), 95, DriverBody.BodyA, Chief, "CA_Idle01_Breathing", 0.4f);
            var off2 = Actor(set, "Second Officer", new Vector3(2.4f, 0, 2.2f), 125, DriverBody.BodyA, OfficerB, "CA_Idle17_Surprised1", 0.4f);
            var p10 = Panel(set, "intro_10", new Vector3(1.1f, 1.65f, -1.6f), new Vector3(5.2f, 1.7f, 0.9f), 48);
            p10.bubbles.Add(Say("CHIEF", "Then bring that balloon down.", 0.3f, 0.18f, 440, 180, Head(chief2)));
            p10.bubbles.Add(Say("OFFICER", "Over the city? You'll put people in danger.", 0.72f, 0.72f, 480, 200, Head(off2)));

            // Panel 11: tight on the chief, sirens on his face.
            var p11 = Panel(set, "intro_11", new Vector3(5.75f, 1.58f, 0.35f), new Vector3(4.6f, 1.6f, 0.66f), 30);
            p11.bubbles.Add(Say("CHIEF", "Fine. Catch them all.", 0.26f, 0.2f, 420, 180, null, new Vector2(0.46f, 0.62f)));
            p11.sfx.Add(Fx("WEE-OOO", 0.78f, 0.8f, -8f, 120f, false));
        }

        static void Alpine(Vector3 o)
        {
            var set = Set("12 Alpine Drop", o);
            var ridge = new GameObject("Mountains").transform; ridge.SetParent(set, false);
            var mf = ridge.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = MountainMesh();
            ridge.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat("Snow", new Color(0.78f, 0.84f, 0.95f));
            Light(set, "Moon", LightType.Directional, new Vector3(0, 50, 0), new Vector3(35, -30, 0), new Color(0.6f, 0.7f, 1f), 1.2f, 0f);

            var lifter = GameObject.Find("Set (copied from the menu)/Showcase Set/Heavy Lifter");
            var carIds = new[] { "", "fireburst", "phobos" };
            var spots = new[] { new Vector3(0, 40, 0), new Vector3(-14, 44, -12), new Vector3(15, 42, -18) };
            for (int i = 0; i < 3; i++)
            {
                var holder = new GameObject("Drop " + (i + 1)).transform; holder.SetParent(set, false);
                holder.localPosition = spots[i]; holder.localRotation = Quaternion.Euler(0, -30, 0);
                if (lifter != null)
                {
                    var copy = Object.Instantiate(lifter, holder);
                    copy.name = "Heavy Lifter";
                    copy.transform.localPosition = Vector3.zero;
                    copy.transform.localRotation = Quaternion.identity;
                    foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                    Transform body = null;
                    foreach (var t in copy.GetComponentsInChildren<Transform>(true)) if (t.name == "Body") { body = t; break; }
                    var rs = (body != null ? body : copy.transform).GetComponentsInChildren<MeshRenderer>(true);
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds; foreach (var mr in rs) b.Encapsulate(mr.bounds);
                        copy.transform.position += holder.position - new Vector3(b.center.x, b.min.y, b.center.z);
                    }
                }
                var car = new GameObject(i == 0 ? "Car (player's)" : "Rival Car").transform; car.SetParent(holder, false);
                car.localPosition = new Vector3(0, -7f, 0);
                car.gameObject.AddComponent<ComicCar>().carId = carIds[i];
                Box(holder, "Cable", new Vector3(0, -3.2f, 0), new Vector3(0.06f, 6.5f, 0.06f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            }
            var balloon = Balloon(set, new Vector3(-24, 48, -40));
            balloon.localRotation = Quaternion.Euler(0, 25, 0);
            var p12 = Panel(set, "intro_12", new Vector3(8, 37.5f, 13), new Vector3(-1, 37, -3), 58);
            p12.captions.Add(Cap("ALPINE.", "RACE ONE.", 0.025f, 0.04f, 360));
            p12.bubbles.Add(Say("SLIM  //  CH13 RADIO", "Welcome to Channel 13. Stay loud.", 0.72f, 0.82f, 560, 200, null, new Vector2(1f, 1.1f), true));
            p12.sfx.Add(Fx("WHUP WHUP WHUP", 0.7f, 0.12f, 6f, 110f, true));
        }

        // ---------------- pieces ----------------

        static Transform Balloon(Transform set, Vector3 at)
        {
            var b = new GameObject("CH13 Balloon").transform; b.SetParent(set, false); b.localPosition = at;
            var env = Primitive(PrimitiveType.Sphere, b, "Envelope", new Vector3(0, 9, 0), new Vector3(14, 16, 14), Glow("Balloon Magenta", Magenta, 0.35f));
            Primitive(PrimitiveType.Sphere, b, "Band", new Vector3(0, 9, 0), new Vector3(14.2f, 3f, 14.2f), Mat("Balloon Ink", new Color(0.05f, 0.03f, 0.08f)));
            Box(b, "Basket", new Vector3(0, -0.5f, 0), new Vector3(2.2f, 1.4f, 2.2f), Mat("Basket", new Color(0.35f, 0.2f, 0.1f)));
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3((i % 2 == 0 ? 1 : -1) * 1f, 1.8f, (i < 2 ? 1 : -1) * 1f);
                Box(b, "Rope " + i, c, new Vector3(0.05f, 3.2f, 0.05f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            }
            Box(b, "Screen Frame", new Vector3(0, -6.2f, 0), new Vector3(12.4f, 7f, 0.4f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            Box(b, "Screen", new Vector3(0, -6.2f, 0.22f), new Vector3(12f, 6.6f, 0.05f), Glow("Screen Cyan", new Color(0.05f, 0.5f, 0.6f), 1.6f));
            Text3D(b, "Screen Text", "CHANNEL 13\n<size=55%>GO LIVE // TONIGHT</size>", new Vector3(0, -6.2f, 0.26f), 2.8f, Color.white);
            for (int i = 0; i < 2; i++) Box(b, "Screen Wire " + i, new Vector3(i == 0 ? -5 : 5, -1.8f, 0), new Vector3(0.06f, 2.4f, 0.06f), Mat("Black", new Color(0.02f, 0.02f, 0.03f)));
            Object.DestroyImmediate(env.GetComponent<Collider>());
            return b;
        }

        static void PhoneToEar(Transform set, ComicActor a, bool left)
        {
            var reach = left ? a.leftReach : a.rightReach;
            float side = left ? -1f : 1f;
            reach.on = true; reach.target = null;
            reach.fromHead = new Vector3(side * 0.1f, -0.05f, 0.04f);
            reach.elbowHint = new Vector3(side * 0.6f, -0.7f, 0.3f);
            var hand = left ? a.leftHand : a.rightHand;
            hand.shape = ComicActor.Shape.Grip;
            var phone = Box(set, a.name + " Phone", Vector3.zero, new Vector3(0.075f, 0.15f, 0.012f), Glow("Phone Screen", Cyan, 1.5f));
            Object.DestroyImmediate(phone.GetComponent<Collider>());
            if (left) { a.leftProp = phone; a.leftPropOffset = new Vector3(0f, 0.08f, 0.03f); }
            else { a.rightProp = phone; a.rightPropOffset = new Vector3(0f, 0.08f, 0.03f); }
        }

        static void Pointing(ComicActor a)
        {
            a.rightReach.on = true; a.rightReach.fromHead = new Vector3(0.3f, 0.75f, 0.35f); a.rightReach.elbowHint = new Vector3(1f, -0.3f, 0f);
            a.rightHand.shape = ComicActor.Shape.Point;
            a.headNod = 0.7f;
        }

        // ---------------- builders ----------------

        static readonly Color Magenta = new Color32(255, 46, 154, 255);
        static readonly Color Cyan = new Color32(46, 242, 255, 255);

        static Transform Set(string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = at;
            return t;
        }

        static void Room(Transform set, Vector3 size, Material floor, Material wall)
        {
            Box(set, "Floor", new Vector3(0, -0.05f, 0), new Vector3(size.x, 0.1f, size.z), floor);
            Box(set, "Ceiling", new Vector3(0, size.y, 0), new Vector3(size.x, 0.1f, size.z), wall);
            Box(set, "Back Wall", new Vector3(0, size.y / 2, -size.z / 2), new Vector3(size.x, size.y, 0.1f), wall);
            Box(set, "Left Wall", new Vector3(-size.x / 2, size.y / 2, 0), new Vector3(0.1f, size.y, size.z), wall);
            Box(set, "Right Wall", new Vector3(size.x / 2, size.y / 2, 0), new Vector3(0.1f, size.y, size.z), wall);
        }

        static Transform Box(Transform parent, string name, Vector3 local, Vector3 scale, Material m)
        {
            return Primitive(PrimitiveType.Cube, parent, name, local, scale, m);
        }

        static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 local, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        static void Light(Transform parent, string name, LightType type, Vector3 local, Vector3 euler, Color c, float intensity, float range, float angle = 45f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = type; l.color = c; l.intensity = intensity;
            if (type != LightType.Directional) l.range = range;
            if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.6f; }
            l.shadows = type == LightType.Point ? LightShadows.None : LightShadows.Soft;
        }

        static void Text3D(Transform parent, string name, string text, Vector3 local, float size, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var t = go.AddComponent<TextMeshPro>();
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ShiftRush/Art/Fonts/BigShouldersDisplay-Black SDF.asset");
            t.text = text; t.fontSize = size * 10f; t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(size * 12f, size * 6f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>The tail target: the lettering aims at the actor's head bone.</summary>
        static Transform Head(ComicActor a) { return a.transform; }

        static ComicActor Actor(Transform set, string name, Vector3 local, float yaw, DriverBody body, string[] look, string clip, float t)
        {
            var go = new GameObject(name);
            go.transform.SetParent(set, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var a = go.AddComponent<ComicActor>();
            a.outfit = ComicActor.Outfit.Fixed;
            a.body = body;
            a.pieces.Add(new ComicActor.Piece { slot = DriverSlot.Helmet, itemId = "" });
            if (look != null)
                foreach (var s in look)
                {
                    var kv = s.Split('=');
                    a.pieces.Add(new ComicActor.Piece { slot = (DriverSlot)System.Enum.Parse(typeof(DriverSlot), kv[0]), itemId = kv[1] });
                }
            a.pose = Clip(clip);
            a.poseTime = t;
            return a;
        }

        static AnimationClip Clip(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name))
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    var c = o as AnimationClip;
                    if (c != null && !c.name.StartsWith("__preview")) return c;
                }
            Debug.LogWarning("[COMIC] No clip " + name);
            return null;
        }

        static GameObject PrefabByName(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        static ComicLettering Panel(Transform set, string name, Vector3 local, Vector3 lookAtLocal, float fov, Vector2Int size = default(Vector2Int))
        {
            var go = new GameObject(name);
            go.transform.SetParent(set, false);
            go.transform.localPosition = local;
            go.transform.LookAt(set.TransformPoint(lookAtLocal));
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            cam.enabled = false; // only renders when captured, so the Game view keeps the main panel
            var pc = go.AddComponent<ComicPanelCamera>();
            pc.panelName = name;
            if (size != default(Vector2Int)) pc.panelSize = size;
            var l = ComicStageBuilder.AddLettering(cam);
            l.captions.Clear(); l.bubbles.Clear(); l.sfx.Clear();
            return l;
        }

        static ComicLettering.Caption Cap(string title, string body, float x, float y, float width)
        {
            return new ComicLettering.Caption { title = title, body = body, at = new Vector2(x, y), width = width };
        }

        static ComicLettering.Bubble Say(string speaker, string line, float x, float y, float w, float h, Transform who, Vector2 tailTo = default(Vector2), bool radio = false)
        {
            return new ComicLettering.Bubble
            {
                speaker = speaker, line = line, radio = radio, at = new Vector2(x, y), size = new Vector2(w, h),
                tail = true, speaker3D = who, tailTo = tailTo == default(Vector2) ? new Vector2(x, y + 0.3f) : tailTo
            };
        }

        static ComicLettering.Sfx Fx(string text, float x, float y, float angle, float size, bool cyan)
        {
            return new ComicLettering.Sfx { text = text, at = new Vector2(x, y), angle = angle, size = size, cyan = cyan };
        }

        static Material Mat(string name, Color c, float smooth = 0.25f)
        {
            string path = MatFolder + "IM_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A self-lit surface (neon, screens, windows): URP Unlit with an HDR colour, so it glows and blooms.</summary>
        static Material Glow(string name, Color c, float strength)
        {
            string path = MatFolder + "IM_Glow " + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            var col = c * strength; col.a = 1f;
            m.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh MountainMesh()
        {
            const string path = MatFolder + "Mesh_IntroMountains.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int n = 90; const float size = 260f;
            var v = new Vector3[n * n]; var uv = new Vector2[n * n]; var tri = new int[(n - 1) * (n - 1) * 6];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x / (float)(n - 1), fz = z / (float)(n - 1);
                    float px = (fx - 0.5f) * size, pz = (fz - 0.5f) * size;
                    float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(fx * 3.1f + 7, fz * 3.1f + 3) * 2f - 1f);
                    float h = Mathf.Pow(ridge, 2.2f) * 38f + Mathf.PerlinNoise(fx * 12f, fz * 12f) * 5f;
                    // A valley down the middle where the track runs.
                    h *= Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(Mathf.Abs(px + pz * 0.3f) / 40f));
                    v[z * n + x] = new Vector3(px, h - 2f, pz);
                    uv[z * n + x] = new Vector2(fx, fz);
                }
            int k = 0;
            for (int z = 0; z < n - 1; z++)
                for (int x = 0; x < n - 1; x++)
                {
                    int i = z * n + x;
                    tri[k++] = i; tri[k++] = i + n; tri[k++] = i + 1;
                    tri[k++] = i + 1; tri[k++] = i + n; tri[k++] = i + n + 1;
                }
            mesh = new Mesh { name = "Intro Mountains", vertices = v, uv = uv, triangles = tri };
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
