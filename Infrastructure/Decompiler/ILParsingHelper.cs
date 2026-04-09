using System.Reflection.Metadata;

namespace ILSpy.Mcp.Infrastructure.Decompiler;

/// <summary>
/// Shared IL bytecode parsing helpers used by both ILSpyCrossReferenceService and ILSpySearchService.
/// </summary>
internal static class ILParsingHelper
{
    /// <summary>
    /// Reads an IL opcode, handling both single-byte and two-byte (0xFE prefix) opcodes.
    /// </summary>
    internal static ILOpCode ReadILOpCode(ref BlobReader reader)
    {
        byte b = reader.ReadByte();
        if (b == 0xFE && reader.RemainingBytes > 0)
        {
            byte b2 = reader.ReadByte();
            return (ILOpCode)(0xFE00 | b2);
        }
        return (ILOpCode)b;
    }

    /// <summary>
    /// Returns true for opcodes that take an inline metadata token operand.
    /// </summary>
    internal static bool IsTokenReferenceOpCode(ILOpCode opCode)
    {
        return opCode switch
        {
            ILOpCode.Call => true,
            ILOpCode.Callvirt => true,
            ILOpCode.Newobj => true,
            ILOpCode.Ldfld => true,
            ILOpCode.Stfld => true,
            ILOpCode.Ldsfld => true,
            ILOpCode.Stsfld => true,
            ILOpCode.Ldflda => true,
            ILOpCode.Ldsflda => true,
            ILOpCode.Ldtoken => true,
            ILOpCode.Ldftn => true,
            ILOpCode.Ldvirtftn => true,
            _ => false
        };
    }

    /// <summary>
    /// Skips the operand of an IL instruction based on its opcode.
    /// </summary>
    internal static void SkipOperand(ref BlobReader reader, ILOpCode opCode)
    {
        switch (GetOperandSize(opCode))
        {
            case 0: break;
            case 1: reader.ReadByte(); break;
            case 2: reader.ReadInt16(); break;
            case 4: reader.ReadInt32(); break;
            case 8: reader.ReadInt64(); break;
            case -1: // Switch instruction
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    reader.ReadInt32();
                break;
        }
    }

    /// <summary>
    /// Returns the operand size for an IL opcode. Returns -1 for switch (variable length).
    /// </summary>
    internal static int GetOperandSize(ILOpCode opCode)
    {
        return opCode switch
        {
            // No operand
            ILOpCode.Nop or ILOpCode.Break or ILOpCode.Ldarg_0 or ILOpCode.Ldarg_1 or ILOpCode.Ldarg_2
            or ILOpCode.Ldarg_3 or ILOpCode.Ldloc_0 or ILOpCode.Ldloc_1 or ILOpCode.Ldloc_2
            or ILOpCode.Ldloc_3 or ILOpCode.Stloc_0 or ILOpCode.Stloc_1 or ILOpCode.Stloc_2
            or ILOpCode.Stloc_3 or ILOpCode.Ldnull or ILOpCode.Ldc_i4_m1 or ILOpCode.Ldc_i4_0
            or ILOpCode.Ldc_i4_1 or ILOpCode.Ldc_i4_2 or ILOpCode.Ldc_i4_3 or ILOpCode.Ldc_i4_4
            or ILOpCode.Ldc_i4_5 or ILOpCode.Ldc_i4_6 or ILOpCode.Ldc_i4_7 or ILOpCode.Ldc_i4_8
            or ILOpCode.Dup or ILOpCode.Pop or ILOpCode.Ret or ILOpCode.Ldind_i1 or ILOpCode.Ldind_u1
            or ILOpCode.Ldind_i2 or ILOpCode.Ldind_u2 or ILOpCode.Ldind_i4 or ILOpCode.Ldind_u4
            or ILOpCode.Ldind_i8 or ILOpCode.Ldind_i or ILOpCode.Ldind_r4 or ILOpCode.Ldind_r8
            or ILOpCode.Ldind_ref or ILOpCode.Stind_ref or ILOpCode.Stind_i1 or ILOpCode.Stind_i2
            or ILOpCode.Stind_i4 or ILOpCode.Stind_i8 or ILOpCode.Stind_r4 or ILOpCode.Stind_r8
            or ILOpCode.Add or ILOpCode.Sub or ILOpCode.Mul or ILOpCode.Div or ILOpCode.Div_un
            or ILOpCode.Rem or ILOpCode.Rem_un or ILOpCode.And or ILOpCode.Or or ILOpCode.Xor
            or ILOpCode.Shl or ILOpCode.Shr or ILOpCode.Shr_un or ILOpCode.Neg or ILOpCode.Not
            or ILOpCode.Conv_i1 or ILOpCode.Conv_i2 or ILOpCode.Conv_i4 or ILOpCode.Conv_i8
            or ILOpCode.Conv_r4 or ILOpCode.Conv_r8 or ILOpCode.Conv_u4 or ILOpCode.Conv_u8
            or ILOpCode.Conv_r_un or ILOpCode.Throw or ILOpCode.Conv_ovf_i1_un or ILOpCode.Conv_ovf_i2_un
            or ILOpCode.Conv_ovf_i4_un or ILOpCode.Conv_ovf_i8_un or ILOpCode.Conv_ovf_u1_un
            or ILOpCode.Conv_ovf_u2_un or ILOpCode.Conv_ovf_u4_un or ILOpCode.Conv_ovf_u8_un
            or ILOpCode.Conv_ovf_i_un or ILOpCode.Conv_ovf_u_un or ILOpCode.Ldlen
            or ILOpCode.Ldelem_i1 or ILOpCode.Ldelem_u1 or ILOpCode.Ldelem_i2 or ILOpCode.Ldelem_u2
            or ILOpCode.Ldelem_i4 or ILOpCode.Ldelem_u4 or ILOpCode.Ldelem_i8 or ILOpCode.Ldelem_i
            or ILOpCode.Ldelem_r4 or ILOpCode.Ldelem_r8 or ILOpCode.Ldelem_ref
            or ILOpCode.Stelem_i or ILOpCode.Stelem_i1 or ILOpCode.Stelem_i2 or ILOpCode.Stelem_i4
            or ILOpCode.Stelem_i8 or ILOpCode.Stelem_r4 or ILOpCode.Stelem_r8 or ILOpCode.Stelem_ref
            or ILOpCode.Conv_ovf_i1 or ILOpCode.Conv_ovf_u1 or ILOpCode.Conv_ovf_i2 or ILOpCode.Conv_ovf_u2
            or ILOpCode.Conv_ovf_i4 or ILOpCode.Conv_ovf_u4 or ILOpCode.Conv_ovf_i8 or ILOpCode.Conv_ovf_u8
            or ILOpCode.Ckfinite or ILOpCode.Conv_u2 or ILOpCode.Conv_u1 or ILOpCode.Conv_i
            or ILOpCode.Conv_ovf_i or ILOpCode.Conv_ovf_u or ILOpCode.Add_ovf or ILOpCode.Add_ovf_un
            or ILOpCode.Mul_ovf or ILOpCode.Mul_ovf_un or ILOpCode.Sub_ovf or ILOpCode.Sub_ovf_un
            or ILOpCode.Endfinally or ILOpCode.Stind_i or ILOpCode.Conv_u or ILOpCode.Rethrow
            or ILOpCode.Refanytype or ILOpCode.Readonly
            => 0,

            // 1-byte operand
            ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s
            or ILOpCode.Ldloca_s or ILOpCode.Stloc_s or ILOpCode.Ldc_i4_s
            or ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s
            or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s or ILOpCode.Ble_s or ILOpCode.Blt_s
            or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s or ILOpCode.Ble_un_s
            or ILOpCode.Blt_un_s or ILOpCode.Leave_s or ILOpCode.Unaligned
            => 1,

            // 2-byte operand
            ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca
            or ILOpCode.Stloc
            => 2,

            // 4-byte operand
            ILOpCode.Br or ILOpCode.Brfalse or ILOpCode.Brtrue
            or ILOpCode.Beq or ILOpCode.Bge or ILOpCode.Bgt or ILOpCode.Ble or ILOpCode.Blt
            or ILOpCode.Bne_un or ILOpCode.Bge_un or ILOpCode.Bgt_un or ILOpCode.Ble_un or ILOpCode.Blt_un
            or ILOpCode.Leave or ILOpCode.Ldc_i4 or ILOpCode.Ldc_r4
            or ILOpCode.Jmp or ILOpCode.Call or ILOpCode.Calli or ILOpCode.Callvirt
            or ILOpCode.Cpobj or ILOpCode.Ldobj or ILOpCode.Ldstr or ILOpCode.Newobj
            or ILOpCode.Castclass or ILOpCode.Isinst or ILOpCode.Unbox
            or ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld
            or ILOpCode.Ldsflda or ILOpCode.Stsfld or ILOpCode.Stobj
            or ILOpCode.Box or ILOpCode.Newarr or ILOpCode.Ldelema or ILOpCode.Ldelem
            or ILOpCode.Stelem or ILOpCode.Unbox_any
            or ILOpCode.Refanyval or ILOpCode.Mkrefany
            or ILOpCode.Ldtoken or ILOpCode.Ldftn or ILOpCode.Ldvirtftn
            or ILOpCode.Initobj or ILOpCode.Constrained or ILOpCode.Sizeof
            => 4,

            // 8-byte operand
            ILOpCode.Ldc_i8 or ILOpCode.Ldc_r8
            => 8,

            // Switch (variable)
            ILOpCode.Switch => -1,

            // Default: assume no operand
            _ => 0
        };
    }
}
