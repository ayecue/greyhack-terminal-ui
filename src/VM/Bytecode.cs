namespace GreyHackTerminalUI.VM
{
    public enum OpCode : byte
    {
        // Stack operations
        PUSH_CONST,     // Push constant from constant pool: PUSH_CONST <index>
        PUSH_NULL,      // Push null
        PUSH_TRUE,      // Push true
        PUSH_FALSE,     // Push false
        POP,            // Pop and discard top of stack
        DUP,            // Duplicate top of stack

        // Variable operations
        LOAD_VAR,       // Load variable: LOAD_VAR <name_index>
        STORE_VAR,      // Store to variable: STORE_VAR <name_index>
        LOAD_GLOBAL,    // Load from global object: LOAD_GLOBAL <name_index>

        // Arithmetic
        ADD,            // a + b
        SUB,            // a - b
        MUL,            // a * b
        DIV,            // a / b
        MOD,            // a % b
        NEG,            // -a

        // Comparison
        EQ,             // a == b
        NE,             // a != b
        LT,             // a < b
        GT,             // a > b
        LE,             // a <= b
        GE,             // a >= b

        // Logical
        NOT,            // !a
        AND,            // Short-circuit and (uses jump)
        OR,             // Short-circuit or (uses jump)

        // Control flow
        JUMP,           // Unconditional jump: JUMP <offset_high> <offset_low>
        JUMP_IF_FALSE,  // Jump if false: JUMP_IF_FALSE <offset_high> <offset_low>
        JUMP_IF_TRUE,   // Jump if true: JUMP_IF_TRUE <offset_high> <offset_low>

        // Function/method calls
        CALL,           // Call function: CALL <arg_count>
        CALL_METHOD,    // Call method: CALL_METHOD <name_index> <arg_count>
        GET_MEMBER,     // Get member: GET_MEMBER <name_index>
        SET_MEMBER,     // Set member: SET_MEMBER <name_index>

        // Control
        RETURN,         // Return from execution
        RETURN_VALUE,   // Return with value on stack
        HALT            // Stop execution
    }
}
