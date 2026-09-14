using UnityEngine;
using UnityEngine.Assertions;
using GenericSensor = VizProtobufferMessage.VizMessage.Types.GenericSensor;

public static class TestCustomVectorHUD
{
    public static string Run()
    {
        const float tolerance = 1e-5f;
        var parent = new GameObject("Vector test spacecraft");
        var arrow = Object.Instantiate(Resources.Load<GameObject>("Prefabs/SpacecraftHUD/LineObject"), parent.transform, false);
        try
        {
            parent.transform.rotation = Quaternion.Euler(20, 30, 40);
            var hud = arrow.AddComponent<CustomVectorHUD>();
            var line = arrow.GetComponent<LineRenderer>();
            var message = new GenericSensor { Size = 10 };
            message.FieldOfView.Add(0);
            message.Position.Add(new[] { 1.0, 2.0, 3.0 });
            message.NormalVector.Add(new[] { 1.0, 0.0, 0.0 });
            message.Color.Add(new[] { 0, 255, 255, 255 });
            Assert.IsTrue(CustomVectorHUD.IsCustomVector(message));
            hud.ApplyMessage(message);
            Assert.IsTrue(line.enabled);
            Assert.IsTrue((arrow.transform.localPosition - new Vector3(2, 3, -1)).magnitude < tolerance);
            Assert.IsTrue((arrow.transform.forward - parent.transform.TransformDirection(Vector3.back)).magnitude < tolerance);
            Assert.AreEqual(10f * Vector3.one, arrow.transform.localScale);
            Assert.AreEqual(Color.cyan, line.startColor);

            message.NormalVector[0] = 0;
            message.NormalVector[1] = 1;
            hud.ApplyMessage(message);
            Assert.IsTrue((arrow.transform.forward - parent.transform.TransformDirection(Vector3.right)).magnitude < tolerance);
            message.IsHidden = true;
            hud.ApplyMessage(message);
            Assert.IsFalse(line.enabled);
            message.IsHidden = false;
            hud.ApplyMessage(message);
            Assert.IsTrue(line.enabled);
            hud.ConfigureHUDForSpriteMode(true);
            Assert.IsFalse(line.enabled);
            hud.ConfigureHUDForSpriteMode(false);
            Assert.IsTrue(line.enabled);

            message.NormalVector[1] = 0;
            hud.ApplyMessage(message);
            Assert.IsFalse(line.enabled);
            message.NormalVector[0] = double.NaN;
            hud.ApplyMessage(message);
            Assert.IsFalse(line.enabled);
            message.FieldOfView[0] = 10;
            Assert.IsFalse(CustomVectorHUD.IsCustomVector(message));
            return "\t All custom vector HUD tests passed.\n";
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }
}
