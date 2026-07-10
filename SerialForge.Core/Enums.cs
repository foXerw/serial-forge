namespace SerialForge.Core;

public enum FieldKind { Literal, Value, Computed }
public enum CodecType { U8, U16, U32, Bytes, String, Enum, Raw }
public enum ByteOrder { Little, Big }
public enum FramingMode { LengthPrefix, Delimiter, Timeout }
