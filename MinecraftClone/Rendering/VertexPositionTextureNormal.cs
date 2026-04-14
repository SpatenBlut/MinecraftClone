using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Vertex-Format für alle Block-Flächen: Position, UV, Normal und Biom-Tint.
/// </summary>
public struct BlockVertex : IVertexType
{
    public Vector3 Position;
    public Vector2 TextureCoordinate;
    public Vector3 Normal;
    public Color   Color;   // Biom-Tint (z.B. Gras-Grün, Blätter-Grün)

    public BlockVertex(Vector3 position, Vector2 texCoord, Vector3 normal, Color color)
    {
        Position          = position;
        TextureCoordinate = texCoord;
        Normal            = normal;
        Color             = color;
    }

    // Offsets: 0=Position(12), 12=TexCoord(8), 20=Normal(12), 32=Color(4) → 36 Bytes gesamt
    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement( 0, VertexElementFormat.Vector3, VertexElementUsage.Position,           0),
        new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate,  0),
        new VertexElement(20, VertexElementFormat.Vector3, VertexElementUsage.Normal,             0),
        new VertexElement(32, VertexElementFormat.Color,   VertexElementUsage.Color,              0)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
