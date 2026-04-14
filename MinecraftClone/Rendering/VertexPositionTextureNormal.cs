using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public struct VertexPositionTextureNormal : IVertexType
{
    public Vector3 Position;
    public Vector2 TextureCoordinate;
    public Vector3 Normal;

    public VertexPositionTextureNormal(Vector3 position, Vector2 textureCoordinate, Vector3 normal)
    {
        Position = position;
        TextureCoordinate = textureCoordinate;
        Normal = normal;
    }

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(sizeof(float) * 5, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}
