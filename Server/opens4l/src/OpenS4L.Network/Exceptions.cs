using System;
using System.Collections.Generic;
using OpenS4L.Blub;

namespace OpenS4L.Network
{
    public class OpenS4LBadFormatException : Exception
    {
        public OpenS4LBadFormatException(Type type)
            : base($"Bad format in {type.Name}")
        {
        }

        public OpenS4LBadFormatException(Type type, IEnumerable<byte> data)
            : base($"Bad format in {type.Name}: {data.ToHexString()}")
        {
        }
    }

    public class OpenS4LBadOpCodeException : Exception
    {
        public OpenS4LBadOpCodeException(ushort opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(AuthOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(ChatOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(GameOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(GameRuleOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(RelayOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }

        public OpenS4LBadOpCodeException(EventOpCode opCode)
            : base($"Bad opCode: {opCode}")
        {
        }
    }
}
