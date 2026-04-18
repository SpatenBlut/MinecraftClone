using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public class PlayerModel
{
    private readonly GraphicsDevice _gd;
    private readonly BasicEffect    _effect;

    // Body parts (torso, arms, legs) = 5 × 36 = 180 verts starting at 0
    // Head                           = 1 × 36 =  36 verts starting at 180
    private readonly VertexPositionColor[] _verts = new VertexPositionColor[216];

    private static readonly Color SkinColor  = new(0xF9, 0xB3, 0x8F);
    private static readonly Color ShirtColor = new(0x7B, 0xA0, 0x5B);
    private static readonly Color PantsColor = new(0x3B, 0x68, 0xB5);

    public PlayerModel(GraphicsDevice gd)
    {
        _gd = gd;
        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            LightingEnabled    = false,
            TextureEnabled     = false,
        };
        BuildGeometry();
    }

    private void BuildGeometry()
    {
        int i = 0;

        void Box(float x0, float x1, float y0, float y1, float z0, float z1, Color col)
        {
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float bright)
            {
                var fc = new Color((int)(col.R * bright), (int)(col.G * bright), (int)(col.B * bright));
                _verts[i++] = new(a, fc); _verts[i++] = new(b, fc); _verts[i++] = new(c, fc);
                _verts[i++] = new(a, fc); _verts[i++] = new(c, fc); _verts[i++] = new(d, fc);
            }

            var lll = new Vector3(x0, y0, z0); var llh = new Vector3(x0, y0, z1);
            var lhl = new Vector3(x0, y1, z0); var lhh = new Vector3(x0, y1, z1);
            var hll = new Vector3(x1, y0, z0); var hlh = new Vector3(x1, y0, z1);
            var hhl = new Vector3(x1, y1, z0); var hhh = new Vector3(x1, y1, z1);

            Quad(lhh, hhh, hhl, lhl, 1.0f); // +Y top
            Quad(hll, lll, llh, hlh, 0.5f); // -Y bottom
            Quad(llh, hlh, hhh, lhh, 0.8f); // +Z front
            Quad(hll, lll, lhl, hhl, 0.8f); // -Z back
            Quad(hlh, hll, hhl, hhh, 0.6f); // +X side
            Quad(lll, llh, lhh, lhl, 0.6f); // -X side
        }

        // Body parts FIRST (verts 0–179) — use body yaw matrix
        // Local space: feet at origin, model faces +Z, body-right = -X.
        Box(-0.250f,  0.250f, 0.750f, 1.500f, -0.125f,  0.125f, ShirtColor); // torso
        Box(-0.500f, -0.250f, 0.750f, 1.500f, -0.125f,  0.125f, SkinColor);  // right arm
        Box( 0.250f,  0.500f, 0.750f, 1.500f, -0.125f,  0.125f, SkinColor);  // left arm
        Box(-0.250f,  0.000f, 0.000f, 0.750f, -0.125f,  0.125f, PantsColor); // right leg
        Box( 0.000f,  0.250f, 0.000f, 0.750f, -0.125f,  0.125f, PantsColor); // left leg

        // Head LAST (verts 180–215) — use head world matrix (own yaw + pitch)
        Box(-0.250f,  0.250f, 1.500f, 2.000f, -0.250f,  0.250f, SkinColor);
    }

    // footPos   : world-space foot position (player RenderPosition).
    // bodyYaw   : smoothed body yaw (radians).
    // headYaw   : camera yaw  (radians) — head follows camera immediately.
    // headPitch : camera pitch (radians) — head nods with camera.
    public void Draw(Vector3 footPos, float bodyYaw, float headYaw, float headPitch,
                     Matrix view, Matrix projection)
    {
        // Body world matrix: rotate +Z-facing model to bodyYaw direction, translate to world.
        Matrix bodyWorld = Matrix.CreateRotationY(MathHelper.PiOver2 - bodyYaw)
                         * Matrix.CreateTranslation(footPos);

        // Head local transform: pivot at (0, 1.5, 0) — rotate head relative to body,
        // then layer on top of bodyWorld so the head lives in the same world coordinate.
        float headYawDelta = WrapAngle(headYaw - bodyYaw);
        Matrix headLocal =
              Matrix.CreateTranslation(0f, -1.5f, 0f)     // move to pivot
            * Matrix.CreateRotationX(-headPitch)           // pitch (negate: our pitch+ = look up)
            * Matrix.CreateRotationY(-headYawDelta)        // yaw relative to body (negated: MonoGame rotation direction)
            * Matrix.CreateTranslation(0f,  1.5f, 0f);    // back to feet-origin space

        Matrix headWorld = headLocal * bodyWorld;

        var prevRaster = _gd.RasterizerState;
        var prevDepth  = _gd.DepthStencilState;
        var prevBlend  = _gd.BlendState;
        _gd.RasterizerState   = RasterizerState.CullNone;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;

        // Body parts (5 boxes, verts 0–179 = 60 triangles)
        _effect.World      = bodyWorld;
        _effect.View       = view;
        _effect.Projection = projection;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _verts, 0, 60);
        }

        // Head (1 box, verts 180–215 = 12 triangles)
        _effect.World = headWorld;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _verts, 180, 12);
        }

        _gd.RasterizerState   = prevRaster;
        _gd.DepthStencilState = prevDepth;
        _gd.BlendState        = prevBlend;
    }

    private static float WrapAngle(float r)
    {
        r %= MathHelper.TwoPi;
        if (r >  MathHelper.Pi) r -= MathHelper.TwoPi;
        if (r < -MathHelper.Pi) r += MathHelper.TwoPi;
        return r;
    }
}
