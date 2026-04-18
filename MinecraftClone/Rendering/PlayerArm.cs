using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Core;

namespace MinecraftClone.Rendering;

public class PlayerArm
{
    private readonly GraphicsDevice _gd;
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _verts = new VertexPositionColor[36];

    // MC 1.21 right arm cube: addBox(-3, -2, -2, 4, 12, 4) / 16
    private const float Lx = -3f / 16f, Hx = 1f / 16f;
    private const float Ly = -2f / 16f, Hy = 10f / 16f;
    private const float Lz = -2f / 16f, Hz = 2f / 16f;

    private static readonly Color SkinColor = new(0xF9, 0xB3, 0x8F);

    public PlayerArm(GraphicsDevice gd)
    {
        _gd = gd;
        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false,
        };
        BuildGeometry();
    }

    private void BuildGeometry()
    {
        var c = SkinColor;
        int i = 0;

        void Quad(Vector3 a, Vector3 b, Vector3 c2, Vector3 d)
        {
            _verts[i++] = new(a, c); _verts[i++] = new(b, c); _verts[i++] = new(c2, c);
            _verts[i++] = new(a, c); _verts[i++] = new(c2, c); _verts[i++] = new(d, c);
        }

        var lll = new Vector3(Lx, Ly, Lz); var llh = new Vector3(Lx, Ly, Hz);
        var lhl = new Vector3(Lx, Hy, Lz); var lhh = new Vector3(Lx, Hy, Hz);
        var hll = new Vector3(Hx, Ly, Lz); var hlh = new Vector3(Hx, Ly, Hz);
        var hhl = new Vector3(Hx, Hy, Lz); var hhh = new Vector3(Hx, Hy, Hz);

        Quad(lhl, hhl, hhh, lhh); // +Y (hand end)
        Quad(hll, lll, llh, hlh); // -Y (shoulder end)
        Quad(llh, hlh, hhh, lhh); // +Z (front)
        Quad(hll, lll, lhl, hhl); // -Z (back)
        Quad(hlh, hll, hhl, hhh); // +X (right side)
        Quad(lll, llh, lhh, lhl); // -X (left side)
    }

    // MC 1.21 getFov(..., applyEffects=false): fixed base FOV, never sprint-modified.
    private const float ArmFov = 70f;

    public void Draw(Camera camera)
    {
        float aspect = _gd.Viewport.AspectRatio;
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(ArmFov), aspect, 0.05f, 10f);

        _effect.World      = BuildRightArmWorldMatrix();
        _effect.View       = Matrix.Identity;
        _effect.Projection = projection;

        var prevRaster = _gd.RasterizerState;
        var prevDepth  = _gd.DepthStencilState;
        var prevBlend  = _gd.BlendState;
        _gd.RasterizerState   = RasterizerState.CullNone;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _verts, 0, 12);
        }

        _gd.RasterizerState   = prevRaster;
        _gd.DepthStencilState = prevDepth;
        _gd.BlendState        = prevBlend;
    }

    // Faithfully mirrors ItemInHandRenderer.renderPlayerArm (right hand, sign=+1).
    // MC PoseStack is column-vector post-multiply (first op = outermost).
    // MonoGame is row-vector, so the multiply order is reversed relative to MC's call order.
    private static Matrix BuildRightArmWorldMatrix()
    {
        const float Sign = 1f; // right hand

        return
              Matrix.CreateTranslation(-5f / 16f, 2f / 16f, 0f)                  // step 10 — ModelPart.translateAndRotate: PartPose.offset(-5, 2, 0)
            * Matrix.CreateTranslation(Sign * 5.6f, 0f, 0f)                      // step  9
            * Matrix.CreateRotationY(MathHelper.ToRadians(Sign * -135f))         // step  8
            * Matrix.CreateRotationX(MathHelper.ToRadians(200f))                 // step  7
            * Matrix.CreateRotationZ(MathHelper.ToRadians(Sign * 120f))          // step  6
            * Matrix.CreateTranslation(Sign * -1f, 3.6f, 3.5f)                   // step  5
            * Matrix.CreateRotationZ(0f)                                         // step  4 — swing-anim hook (sign * swingMagnitude * -20°)
            * Matrix.CreateRotationY(0f)                                         // step  3 — bob-anim hook   (sign * bobMagnitude   * +70°)
            * Matrix.CreateRotationY(MathHelper.ToRadians(Sign * 45f))           // step  2
            * Matrix.CreateTranslation(Sign * 0.64f, -0.6f, -0.72f);             // step  1 — ItemInHandRenderer camera-space offset (outermost)
    }
}
