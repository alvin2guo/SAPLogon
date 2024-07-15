﻿using SAPTools.LogonTicket.Extensions;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;

using static SAPTools.LogonTicket.Extensions.InfoUnitExtensions;

namespace SAPTools.LogonTicket;
public class InfoUnit {
    public InfoUnitID ID { get; set; }
    public byte[] Content { get; set; }

    public InfoUnit(InfoUnitID id, byte[] data) =>
        (ID, Content) = (id, data);

    public InfoUnit(InfoUnitID id, byte data) =>
        (ID, Content) = (id, [data]);

    public InfoUnit(InfoUnitID id, string data) =>
        (ID, Content) = (id, DetermineEncoding(id).GetBytes(data));

    public InfoUnit(InfoUnitID id, string data, Encoding enc) =>
        (ID, Content) = (id, DetermineEncoding(id, enc).GetBytes(data));

    public InfoUnit(InfoUnitID id, SAPLanguage data, Encoding enc) =>
        (ID, Content) = (id, enc.GetBytes(SAPLanguageExtensions.ToCode(data)));

    public InfoUnit(InfoUnitID id, DateTime data, Encoding enc) =>
        (ID, Content) = (id, enc.GetBytes(data.ToString(CreationDateFormat)));

    public InfoUnit(InfoUnitID id, SignedCms data) =>
        (ID, Content) = (id, data.Encode());

    public InfoUnit(InfoUnitID id, uint data) =>
        (ID, Content) = (id, new byte[] {
            (byte)(data >> 24), (byte)(data >> 16),
            (byte)(data >> 8), (byte)data });

    private const string CreationDateFormat = "yyyyMMddHHmm";

    public virtual void WriteTo(Stream @out) {
        // Ensure the content length does not exceed ushort.MaxValue - 3
        if(Content!.Length > UInt16.MaxValue - 3) throw new InvalidOperationException("Content is too large.");

        ushort totalLength = (ushort)(3 + Content.Length); // Total length calculation
        byte[] buffer = new byte[totalLength];

        buffer[0] = (byte)ID!; // ID
        buffer[1] = (byte)(Content.Length >> 8); // High byte of content length
        buffer[2] = (byte)Content.Length; // Low byte of content length
        Array.Copy(Content, 0, buffer, 3, Content.Length); // Content

        @out.Write(buffer, 0, buffer.Length); // Write buffer to stream
    }

    public string ToString(Encoding enc) => DetermineEncoding(ID, enc).GetString(Content);

    public object? GetValue(Encoding enc) => InfoUnitExtensions.GetInfoUnitType(ID) switch {
        InfoUnitType.String => ToString(enc),
        InfoUnitType.StringUTF8 => Encoding.UTF8.GetString(Content),
        InfoUnitType.StringASCII => Encoding.ASCII.GetString(Content),
        InfoUnitType.DateString => DateTime.ParseExact(ToString(enc), CreationDateFormat, null),
        InfoUnitType.DateStringUTF8 => DateTime.ParseExact(Encoding.UTF8.GetString(Content), CreationDateFormat, null),
        InfoUnitType.Byte => Content[0],
        InfoUnitType.UnsignedInt => BitConverter.IsLittleEndian ? BitConverter.ToUInt32(Content.Reverse().ToArray(), 0) : BitConverter.ToUInt32(Content, 0),
        InfoUnitType.ByteArray => Content,
        InfoUnitType.Signature => DecodeSignedCms(),
        _ => throw new InvalidOperationException("Unknown InfoUnitType"),
    };

    private SignedCms DecodeSignedCms() {
        SignedCms cms = new();
        cms.Decode(Content);
        return cms;
    }
}
