using Yarn.Unity;

public class PipelineTestCodec : AesHmacStringTableCodec
{
    protected override byte[] GetMasterKey()
    {
        var key = new byte[32];
        for (int i = 0; i < key.Length; i++) { key[i] = (byte)(i * 5 + 11); }
        return key;
    }
}
