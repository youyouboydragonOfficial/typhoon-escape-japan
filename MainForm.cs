using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using System.Media;

namespace TyphoonEscapeJapan;

public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly HashSet<Keys> heldKeys = [];
    private readonly Random rng = new(20301002);
    private readonly List<Storm> storms = [];
    private readonly List<Particle> particles = [];
    private readonly List<Cloud> clouds = [];
    private ScreenMode mode = ScreenMode.Intro;
    private float time;
    private float introTime;
    private float score;
    private float best;
    private float shield = 100;
    private float spawnClock;
    private float hitFlash;
    private float eventClock = 10;
    private int modeIndex;
    private int wave = 1;
    private bool tutorialShown = true;
    private bool pause;
    private Vector2 player = new(0.5f, 0.62f);
    private Vector2 playerVelocity;

    private static readonly ModeSpec[] Modes =
    [
        new("STANDARD", "観測型", "徐々に強くなる台風をかわす"),
        new("CHAOS FORECAST", "予測不能", "予測線が外れる急旋回と急発達"),
        new("LANDFALL RUSH", "連続襲来", "複数の台風が一気に上陸"),
        new("SEASON SCENARIO", "季節変動", "前線・熱帯低気圧・大型台風の波")
    ];

    private readonly Color Ink = Color.FromArgb(235, 247, 255);
    private readonly Color Cyan = Color.FromArgb(94, 226, 255);
    private readonly Color Coral = Color.FromArgb(255, 129, 119);
    private readonly Color Navy = Color.FromArgb(7, 17, 39);

    public MainForm()
    {
        Text = "Typhoon Escape // 日本列島防衛線";
        ClientSize = new Size(1280, 760);
        MinimumSize = new Size(960, 620);
        BackColor = Navy;
        DoubleBuffered = true;
        KeyPreview = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        timer.Tick += (_, _) => { if (!pause) UpdateGame(1f / 60f); Invalidate(); };
        KeyDown += (_, e) => { heldKeys.Add(e.KeyCode); HandleKeyDown(e.KeyCode); };
        KeyUp += (_, e) => heldKeys.Remove(e.KeyCode);
        Resize += (_, _) => Invalidate();
        SeedAtmosphere();
        timer.Start();
    }

    private void SeedAtmosphere()
    {
        for (int i = 0; i < 26; i++) clouds.Add(new Cloud(new(rng.NextSingle(), rng.NextSingle() * .8f), .02f + rng.NextSingle() * .055f, .07f + rng.NextSingle() * .12f, rng.NextSingle() * 6.28f));
        for (int i = 0; i < 80; i++) particles.Add(new Particle(new(rng.NextSingle(), rng.NextSingle()), new((rng.NextSingle() - .5f) * .02f, .01f + rng.NextSingle() * .05f), 1 + rng.NextSingle() * 3, rng.NextSingle() * 4));
    }

    private void HandleKeyDown(Keys key)
    {
        if (key == Keys.Escape) { if (mode == ScreenMode.Intro) Close(); else mode = ScreenMode.Menu; }
        if (mode == ScreenMode.Intro && (key == Keys.Enter || key == Keys.Space)) mode = ScreenMode.Menu;
        else if (mode == ScreenMode.Menu && (key == Keys.Left || key == Keys.A)) modeIndex = (modeIndex + Modes.Length - 1) % Modes.Length;
        else if (mode == ScreenMode.Menu && (key == Keys.Right || key == Keys.D)) modeIndex = (modeIndex + 1) % Modes.Length;
        else if (mode == ScreenMode.Menu && (key == Keys.Enter || key == Keys.Space)) StartRun();
        else if (mode == ScreenMode.Tutorial && (key == Keys.Enter || key == Keys.Space)) { tutorialShown = false; mode = ScreenMode.Playing; }
        else if (mode == ScreenMode.Playing && key == Keys.P) pause = !pause;
        else if (mode == ScreenMode.GameOver && (key == Keys.R || key == Keys.Enter || key == Keys.Space)) StartRun();
    }

    private void StartRun()
    {
        mode = tutorialShown ? ScreenMode.Tutorial : ScreenMode.Playing;
        if (mode == ScreenMode.Tutorial) tutorialShown = false;
        score = 0; shield = 100; wave = 1; spawnClock = 0; eventClock = 8; hitFlash = 0; pause = false;
        player = new(.5f, .63f); playerVelocity = Vector2.Zero; storms.Clear();
        int openingStorms = modeIndex == 2 ? 6 : modeIndex == 1 ? 4 : 3;
        for (int i = 0; i < openingStorms; i++) SpawnStorm(i * .42f + .15f);
    }

    private void UpdateGame(float dt)
    {
        time += dt; introTime += dt;
        foreach (var p in particles) { p.Position += p.Velocity * dt; p.Phase += dt; if (p.Position.Y > 1.05f) p.Position = new(p.Position.X, -.03f); }
        foreach (var c in clouds) { c.Position.X -= c.Speed * dt; if (c.Position.X < -.2f) c.Position.X = 1.1f; }
        if (mode == ScreenMode.Intro) return;
        if (mode != ScreenMode.Playing || pause) return;
        var axis = Vector2.Zero;
        if (heldKeys.Contains(Keys.Left) || heldKeys.Contains(Keys.A)) axis.X -= 1;
        if (heldKeys.Contains(Keys.Right) || heldKeys.Contains(Keys.D)) axis.X += 1;
        if (heldKeys.Contains(Keys.Up) || heldKeys.Contains(Keys.W)) axis.Y -= 1;
        if (heldKeys.Contains(Keys.Down) || heldKeys.Contains(Keys.S)) axis.Y += 1;
        if (axis.LengthSquared() > 0) axis = Vector2.Normalize(axis);
        playerVelocity = Vector2.Lerp(playerVelocity, axis * .34f, .16f);
        player += playerVelocity * dt;
        player.X = Math.Clamp(player.X, .13f, .87f); player.Y = Math.Clamp(player.Y, .19f, .88f);
        score += dt * (12 + wave * 2);
        wave = 1 + (int)(score / 500);
        spawnClock -= dt;
        if (spawnClock <= 0) { SpawnStorm(0); if (modeIndex == 2 && rng.NextDouble() < .48) SpawnStorm(.12f); spawnClock = Math.Max(.55f, 2.1f - wave * .1f); }
        eventClock -= dt;
        if (eventClock <= 0)
        {
            int burst = modeIndex == 1 ? rng.Next(2, 5) : modeIndex == 2 ? rng.Next(4, 8) : (rng.NextDouble() < .45 ? 2 : 1);
            for (int i = 0; i < burst; i++) SpawnStorm(i * .2f, burst > 2 || modeIndex == 1);
            eventClock = Math.Max(5, 15 - wave * .35f) + rng.NextSingle() * 9;
        }
        foreach (var s in storms.ToArray())
        {
            s.Update(dt, time, modeIndex);
            var d = Vector2.Distance(player, s.Position);
            if (d < s.Radius * .85f) { shield -= dt * (20 + wave * 2); hitFlash = .35f; }
            if (s.Position.X < -.2f || s.Position.X > 1.2f || s.Position.Y < -.25f || s.Position.Y > 1.25f) storms.Remove(s);
        }
        hitFlash = Math.Max(0, hitFlash - dt);
        if (shield <= 0) { shield = 0; best = Math.Max(best, score); mode = ScreenMode.GameOver; SystemSounds.Exclamation.Play(); }
    }

    private void SpawnStorm(float delay, bool severe = false)
    {
        int side = rng.Next(4); Vector2 pos = side switch { 0 => new(-.08f, .18f + rng.NextSingle() * .7f), 1 => new(1.08f, .15f + rng.NextSingle() * .75f), 2 => new(.12f + rng.NextSingle() * .76f, -.08f), _ => new(.12f + rng.NextSingle() * .76f, 1.08f) };
        Vector2 target = new(.2f + rng.NextSingle() * .6f, .35f + rng.NextSingle() * .45f);
        bool rapid = severe || modeIndex == 1 && rng.NextDouble() < .65 || (modeIndex == 3 && score > 500 && rng.NextDouble() < .4);
        storms.Add(new Storm(pos, Vector2.Normalize(target - pos), .055f + rng.NextSingle() * .034f, .055f + rng.NextSingle() * .025f, delay, rng.NextSingle() * 4, rapid, rng.NextSingle() * .9f));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        DrawScene(e.Graphics);
        if (mode == ScreenMode.Intro) DrawIntro(e.Graphics);
        else if (mode == ScreenMode.Menu) DrawMenu(e.Graphics);
        else if (mode == ScreenMode.Tutorial) { DrawGame(e.Graphics); DrawTutorial(e.Graphics); }
        else if (mode == ScreenMode.Playing) DrawGame(e.Graphics);
        else DrawGameOver(e.Graphics);
        DrawCredit(e.Graphics);
    }

    private RectangleF R(float x, float y, float w, float h) => new(x * ClientSize.Width, y * ClientSize.Height, w * ClientSize.Width, h * ClientSize.Height);
    private PointF P(Vector2 v) => new(v.X * ClientSize.Width, v.Y * ClientSize.Height);

    private void DrawScene(Graphics g)
    {
        using var bg = new LinearGradientBrush(ClientRectangle, Color.FromArgb(6, 18, 42), Color.FromArgb(19, 76, 113), 90);
        g.FillRectangle(bg, ClientRectangle);
        using var glow = new PathGradientBrush(new[] { new PointF(ClientSize.Width * .52f, ClientSize.Height * .3f), new PointF(ClientSize.Width * .73f, ClientSize.Height * .25f), new PointF(ClientSize.Width * .8f, ClientSize.Height * .65f), new PointF(ClientSize.Width * .35f, ClientSize.Height * .72f) }) { CenterColor = Color.FromArgb(74, 197, 220), SurroundColors = [Color.FromArgb(0, 18, 52), Color.FromArgb(8, 28, 57), Color.FromArgb(7, 22, 49), Color.FromArgb(8, 22, 48)] };
        g.FillRectangle(glow, ClientRectangle);
        using var pen = new Pen(Color.FromArgb(28, 148, 183), 1);
        for (int i = 0; i < 13; i++) { float y = ClientSize.Height * (.2f + i * .064f); g.DrawLine(pen, 0, y, ClientSize.Width, y + ClientSize.Width * .08f); }
        foreach (var c in clouds) DrawCloud(g, c);
        foreach (var p in particles) using (var b = new SolidBrush(Color.FromArgb((int)(50 + 65 * MathF.Abs(MathF.Sin(p.Phase))), 157, 221, 232))) g.FillEllipse(b, R(p.Position.X, p.Position.Y, p.Size / 120, p.Size / 120));
    }

    private void DrawCloud(Graphics g, Cloud c)
    {
        var box = R(c.Position.X, c.Position.Y, c.Width, .055f); using var b = new SolidBrush(Color.FromArgb(14, 224, 245, 255));
        g.FillEllipse(b, box); g.FillEllipse(b, new RectangleF(box.X + box.Width * .2f, box.Y - box.Height * .42f, box.Width * .4f, box.Height * 1.3f)); g.FillEllipse(b, new RectangleF(box.X + box.Width * .5f, box.Y - box.Height * .22f, box.Width * .34f, box.Height * 1.1f));
    }

    private void DrawIntro(Graphics g)
    {
        float fade = Math.Clamp(introTime / 2.2f, 0, 1); using var shade = new SolidBrush(Color.FromArgb((int)(210 * (1 - fade * .35f)), 3, 11, 28)); g.FillRectangle(shade, ClientRectangle);
        var center = new PointF(ClientSize.Width * .66f, ClientSize.Height * .45f); using var ring = new Pen(Color.FromArgb(150, 103, 225, 241), 3);
        for (int i = 0; i < 5; i++) { float rr = 60 + i * 37 + MathF.Sin(time * 1.5f + i) * 6; g.DrawEllipse(ring, center.X - rr, center.Y - rr, rr * 2, rr * 2); }
        DrawJapan(g, new(.65f, .47f), 1.7f, Color.FromArgb(200, 224, 247, 232));
        using var title = new Font("Yu Gothic UI", Math.Max(22, ClientSize.Width / 35), FontStyle.Bold); using var sub = new Font("Yu Gothic UI", Math.Max(10, ClientSize.Width / 92), FontStyle.Regular);
        DrawText(g, "TYHOON ESCAPE", title, Ink, new(.08f, .16f)); DrawText(g, "日本列島防衛線", sub, Color.FromArgb(169, 230, 244), new(.085f, .27f));
        using var line = new Pen(Color.FromArgb((int)(170 * fade), 104, 232, 250), 2); g.DrawLine(line, R(.085f, .34f, .18f, 0).Location, R(.265f, .34f, 0, 0).Location);
        using var small = new Font("Consolas", 10, FontStyle.Regular); DrawText(g, "A PROCEDURAL WEATHER SURVIVAL", small, Color.FromArgb(155, 195, 215), new(.085f, .37f));
        if (introTime > 1.5f) { using var prompt = new Font("Yu Gothic UI", 11, FontStyle.Bold); DrawText(g, "PRESS  ENTER  TO  BEGIN", prompt, Color.FromArgb(220, 255, 255), new(.085f, .78f)); }
    }

    private void DrawMenu(Graphics g)
    {
        using var panel = new SolidBrush(Color.FromArgb(126, 4, 14, 37)); g.FillRectangle(panel, R(.065f, .10f, .37f, .76f)); using var border = new Pen(Color.FromArgb(66, 102, 201, 220), 1); g.DrawRectangle(border, R(.065f, .10f, .37f, .76f));
        DrawText(g, "日本列島防衛線", new("Yu Gothic UI", Math.Max(20, ClientSize.Width / 44), FontStyle.Bold), Ink, new(.10f, .17f));
        DrawText(g, "TYHOON ESCAPE", new("Consolas", Math.Max(12, ClientSize.Width / 76), FontStyle.Bold), Cyan, new(.105f, .255f));
        DrawButton(g, R(.10f, .40f, .28f, .10f), "▶  出撃する", true); DrawButton(g, R(.10f, .54f, .28f, .075f), "？  チュートリアル", false); DrawButton(g, R(.10f, .65f, .28f, .075f), "ESC  終了", false);
        DrawText(g, "Arrow keys / WASD で日本列島を誘導", new("Yu Gothic UI", 10), Color.FromArgb(135, 190, 207), new(.10f, .78f));
        var selected = Modes[modeIndex];
        DrawText(g, "MODE  /  ← → で選択", new("Consolas", 10, FontStyle.Bold), Color.FromArgb(133, 196, 217), new(.55f, .17f));
        DrawText(g, selected.Title, new("Consolas", 20, FontStyle.Bold), Color.FromArgb(255, 224, 170), new(.55f, .22f));
        DrawText(g, selected.Japanese, new("Yu Gothic UI", 15, FontStyle.Bold), Ink, new(.55f, .29f));
        DrawText(g, selected.Description, new("Yu Gothic UI", 10), Color.FromArgb(169, 213, 224), new(.55f, .35f));
        DrawJapan(g, new(.67f, .57f), 2.35f, Color.FromArgb(214, 246, 232)); DrawStorm(g, new(.72f, .37f), .11f, time * .28f, 1.0f);
        DrawText(g, "SEASONAL STORM MODEL", new("Consolas", 11, FontStyle.Bold), Color.FromArgb(135, 196, 217), new(.60f, .75f));
    }

    private void DrawGame(Graphics g)
    {
        foreach (var s in storms) { DrawForecast(g, s); DrawStorm(g, s.Position, s.Radius, s.Angle, s.Strength); }
        DrawJapan(g, player, 1.35f, hitFlash > 0 ? Color.FromArgb(255, 174, 150) : Color.FromArgb(224, 247, 232));
        using var hud = new SolidBrush(Color.FromArgb(170, 3, 13, 32)); g.FillRectangle(hud, R(.03f, .035f, .94f, .09f));
        DrawText(g, "防衛レベル", new("Yu Gothic UI", 10, FontStyle.Bold), Color.FromArgb(142, 202, 220), new(.06f, .052f)); DrawText(g, wave.ToString("00"), new("Consolas", 25, FontStyle.Bold), Ink, new(.06f, .072f));
        DrawText(g, "SCORE", new("Consolas", 10, FontStyle.Bold), Color.FromArgb(142, 202, 220), new(.25f, .052f)); DrawText(g, Math.Floor(score).ToString("000000"), new("Consolas", 22, FontStyle.Bold), Ink, new(.25f, .073f));
        DrawText(g, "安全度", new("Yu Gothic UI", 10, FontStyle.Bold), Color.FromArgb(142, 202, 220), new(.58f, .052f)); using var barBack = new SolidBrush(Color.FromArgb(60, 126, 201, 215)); g.FillRectangle(barBack, R(.65f, .071f, .22f, .018f)); using var bar = new SolidBrush(shield < 35 ? Coral : Cyan); g.FillRectangle(bar, R(.65f, .071f, .22f * shield / 100f, .018f));
        DrawText(g, "P  PAUSE", new("Consolas", 9), Color.FromArgb(135, 190, 207), new(.88f, .08f));
        DrawText(g, "日本列島を台風の目から離せ", new("Yu Gothic UI", 11, FontStyle.Bold), Color.FromArgb(186, 229, 235), new(.06f, .91f));
    }

    private void DrawForecast(Graphics g, Storm s)
    {
        if (s.Delay > 0) return;
        using var pen = new Pen(Color.FromArgb(82, 187, 225, 237), 1.2f) { DashStyle = DashStyle.Dash };
        var points = new List<PointF>(); var pos = s.Position; var dir = s.Direction;
        for (int i = 0; i < 8; i++) { float turn = MathF.Sin(time * .7f + s.Curvature * i) * s.Curvature * .09f; dir = Rotate(dir, turn); pos += dir * (s.Speed * 24); points.Add(P(pos)); }
        if (points.Count > 1) g.DrawLines(pen, points.ToArray());
        using var label = new Font("Consolas", 8, FontStyle.Bold); DrawText(g, s.Rapid ? "RAPID INTENSIFY" : "FORECAST", label, s.Rapid ? Color.FromArgb(255, 189, 141) : Color.FromArgb(135, 201, 220), new(Math.Clamp(s.Position.X, .02f, .82f), Math.Clamp(s.Position.Y - .07f, .14f, .88f)));
    }

    private static Vector2 Rotate(Vector2 v, float radians) => new(v.X * MathF.Cos(radians) - v.Y * MathF.Sin(radians), v.X * MathF.Sin(radians) + v.Y * MathF.Cos(radians));

    private void DrawTutorial(Graphics g)
    {
        using var dim = new SolidBrush(Color.FromArgb(155, 2, 8, 24)); g.FillRectangle(dim, ClientRectangle); var box = R(.20f, .18f, .60f, .58f); using var panel = new SolidBrush(Color.FromArgb(240, 8, 28, 56)); g.FillRectangle(panel, box); using var border = new Pen(Color.FromArgb(113, 226, 241), 2); g.DrawRectangle(border, box);
        DrawText(g, "MISSION BRIEFING", new("Consolas", 16, FontStyle.Bold), Cyan, new(.25f, .24f)); DrawText(g, "チュートリアル", new("Yu Gothic UI", 23, FontStyle.Bold), Ink, new(.25f, .29f));
        DrawText(g, "日本列島を左右・上下に動かして、接近する台風をかわします。", new("Yu Gothic UI", 12), Color.FromArgb(210, 234, 241), new(.25f, .40f));
        DrawText(g, "矢印キー / WASD", new("Yu Gothic UI", 14, FontStyle.Bold), Color.FromArgb(255, 223, 160), new(.25f, .50f)); DrawText(g, "移動", new("Yu Gothic UI", 12), Color.FromArgb(182, 216, 228), new(.56f, .515f));
        DrawText(g, "台風の中心と渦に触れると安全度が下がります。", new("Yu Gothic UI", 12), Color.FromArgb(210, 234, 241), new(.25f, .59f)); DrawText(g, "ENTER / SPACE で出撃", new("Consolas", 12, FontStyle.Bold), Cyan, new(.25f, .68f));
    }

    private void DrawGameOver(Graphics g)
    {
        DrawGame(g); using var dim = new SolidBrush(Color.FromArgb(170, 3, 7, 18)); g.FillRectangle(dim, ClientRectangle); DrawText(g, "防衛線、突破されました", new("Yu Gothic UI", 26, FontStyle.Bold), Color.FromArgb(255, 190, 165), new(.31f, .34f)); DrawText(g, $"SCORE  {Math.Floor(score):000000}", new("Consolas", 18, FontStyle.Bold), Ink, new(.42f, .46f)); DrawText(g, $"BEST   {Math.Floor(best):000000}", new("Consolas", 12), Color.FromArgb(160, 206, 220), new(.44f, .53f)); DrawText(g, "R / ENTER  もう一度出撃", new("Consolas", 12, FontStyle.Bold), Cyan, new(.39f, .65f));
    }

    private void DrawButton(Graphics g, RectangleF rect, string label, bool active)
    { using var b = new SolidBrush(active ? Color.FromArgb(32, 147, 190, 213) : Color.FromArgb(20, 120, 174, 191)); g.FillRectangle(b, rect); using var p = new Pen(active ? Cyan : Color.FromArgb(63, 143, 181), 1); g.DrawRectangle(p, rect); DrawText(g, label, new("Yu Gothic UI", 13, FontStyle.Bold), active ? Ink : Color.FromArgb(185, 221, 229), new(rect.X / ClientSize.Width + .02f, rect.Y / ClientSize.Height + .026f)); }

    private void DrawStorm(Graphics g, Vector2 pos, float radius, float angle, float strength)
    {
        var c = P(pos); float r = radius * ClientSize.Width; using var glow = new SolidBrush(Color.FromArgb(24, 101, 185, 226)); g.FillEllipse(glow, c.X - r * 1.8f, c.Y - r * 1.8f, r * 3.6f, r * 3.6f); using var core = new SolidBrush(Color.FromArgb(60, 30, 71, 112)); g.FillEllipse(core, c.X - r, c.Y - r, r * 2, r * 2); using var pen = new Pen(Color.FromArgb(188, 115, 221, 240), Math.Max(1, r * .045f));
        for (int ring = 0; ring < 3; ring++) { var pts = new List<PointF>(); for (int i = 0; i <= 90; i++) { float t = i / 90f * 5.9f + angle + ring * 2.1f; float rr = r * (0.1f + ring * .23f) + r * .043f * i; pts.Add(new(c.X + MathF.Cos(t) * rr, c.Y + MathF.Sin(t) * rr * .72f)); } if (pts.Count > 1) g.DrawLines(pen, pts.ToArray()); }
        using var eye = new SolidBrush(Color.FromArgb(220, 223, 250, 244)); g.FillEllipse(eye, c.X - r * .12f, c.Y - r * .09f, r * .24f, r * .18f);
    }

    private void DrawJapan(Graphics g, Vector2 pos, float scale, Color fill)
    {
        float s = ClientSize.Width * .026f * scale; var points = new[] { new PointF(-.22f,-1.0f), new PointF(.02f,-.82f), new PointF(.12f,-.50f), new PointF(.30f,-.30f), new PointF(.28f,-.05f), new PointF(.10f,.18f), new PointF(.20f,.40f), new PointF(.05f,.60f), new PointF(-.10f,.92f), new PointF(-.25f,1.0f), new PointF(-.18f,.61f), new PointF(-.31f,.32f), new PointF(-.20f,.07f), new PointF(-.36f,-.20f), new PointF(-.31f,-.52f) };
        var pts = points.Select(v => new PointF(ClientSize.Width * pos.X + v.X * s, ClientSize.Height * pos.Y + v.Y * s * 1.7f)).ToArray(); using var shadow = new SolidBrush(Color.FromArgb(50, 0, 15, 20)); g.FillPolygon(shadow, pts.Select(v => new PointF(v.X + 8, v.Y + 10)).ToArray()); using var b = new SolidBrush(fill); g.FillPolygon(b, pts); using var p = new Pen(Color.FromArgb(255, 160, 235, 218), Math.Max(1, s * .045f)); g.DrawPolygon(p, pts);
    }

    private void DrawText(Graphics g, string text, Font font, Color color, PointF normalized)
    { using var b = new SolidBrush(color); g.DrawString(text, font, b, normalized.X * ClientSize.Width, normalized.Y * ClientSize.Height); }
    private void DrawCredit(Graphics g)
    { using var f = new Font("Consolas", 9); DrawText(g, "©by youyouboydragon", f, Color.FromArgb(150, 206, 218), new(.80f, .955f)); }

    private enum ScreenMode { Intro, Menu, Tutorial, Playing, GameOver }
    private sealed record ModeSpec(string Title, string Japanese, string Description);
    private sealed class Storm(Vector2 position, Vector2 direction, float speed, float radius, float delay, float angle, bool rapid, float curvature)
    {
        public Vector2 Position = position; public Vector2 Direction = direction; public float Speed = speed; public float Radius = radius; public float Delay = delay; public float Angle = angle; public float Strength = 1; public bool Rapid = rapid; public float Curvature = curvature;
        public void Update(float dt, float t, int selectedMode)
        {
            if (Delay > 0) { Delay -= dt; return; }
            float turn = MathF.Sin(t * (.35f + Curvature) + Angle) * Curvature * dt * (selectedMode == 1 ? 2.7f : 1.4f);
            Direction = Vector2.Normalize(Rotate(Direction, turn)); Position += Direction * Speed * dt * (Rapid ? 1.22f : 1f); Angle += dt * (Rapid ? 1.55f : 1.1f);
            if (Rapid) { Radius = Math.Min(.15f, Radius + dt * .0016f); Speed = Math.Min(.105f, Speed + dt * .00032f); }
            Strength = .75f + .25f * MathF.Sin(t * 1.5f + Angle);
        }
    }
    private sealed class Particle(Vector2 position, Vector2 velocity, float size, float phase) { public Vector2 Position = position; public Vector2 Velocity = velocity; public float Size = size; public float Phase = phase; }
    private sealed class Cloud(Vector2 position, float speed, float width, float phase) { public Vector2 Position = position; public float Speed = speed; public float Width = width; public float Phase = phase; }
}
