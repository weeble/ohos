import sys
from optparse import OptionParser

description = "Translate uSCPD files to XML."
command_group = "Developer tools"

# Micro SCPD (uSCPD) parser
#
# Micro SCPD is a concise text format to describe a UPNP service.
#
# Example state variables:
# 
# # Evented integer state variable with range 0 to 100 inclusive in steps of 1, default 50:
# var Volume : int [0:100:1] = 50;
#
# # Evented string state variable with allowed values:
# var Flavour : string ["vanilla", "chocolate", "strawberry"];
#
# # Evented 32-bit unsigned integer state variable with no default or range:
# var Identity : ui4;
#
# # Non-evented state variable A_ARG_TYPE_Colour with allowed values "red", "green" and "blue":
# type $Colour : string ["red", "green", "blue"];
#
# Syntax for evented and unevented state variables is the same except for the
# "var" or "type" indicator. Any time an identifier starts with "$", it is expanded
# to "A_ARG_TYPE_". E.g. $Colour === A_ARG_TYPE_Colour
#
# Example actions:
#
# action DispenseIceCream(Flavour : in Flavour) = Success : $Success;
# action Create(PresetXml : in $PresetXml, PresetId : out $PresetId);
# action ComplexActionWithManyArgs(
#     Size : in $Metres,
#     TimeLimit : in $Seconds,
#     DataXml : in $DataXml,
#     Elapsed : out $Seconds,
#     Stats : out $DataXml);
#
# If the action has "= RetValue : RetVariable" at the end, it is interpreted
# as an extra out argument with the "retval" marker, and inserted into the
# argument list before the first output argument.


#########
# Lexer #
#########

START = object()
END = object()
SIMPLE_OPERATORS = "()[]{}:;=,"
EOS_TOKEN = ("OP", ";")

class StateMachine(object):
    """
    State machine.
    Subclasses should override start_state and add
    other states as methods taking a message and
    returning a (bound) state method for the next
    state.
    """
    def __init__(self):
        self._state = START
    def step(self, msg):
        '''
        Receive a message and transition to the
        next state accordingly.
        '''
        if self._state is START:
            self._state = self.start_state
        if self._state is END:
            raise Exception("Too much input")
        self._state = self._state(msg)
    def run(self, iterable):
        '''
        Receive a sequence of messages and change
        state accordingly.
        '''
        for msg in iterable:
            if self._state is END:
                raise Exception("Too much input")
            self.step(msg)
        if self._state is END:
            raise Exception("Too much input")
        self.step(None)
        if self._state is not END:
            raise Exception("Unexpected end of input")
    def start_state(self, msg):
        return END

def dont_end(msg):
    if msg is None:
        raise Exception("Unexpected end of input")

class LexMachine(StateMachine):
    def __init__(self, tokeneater):
        StateMachine.__init__(self)
        self.tokeneater=tokeneater
    def submachine(self, cls, msg, end_state=None):
        if end_state is None:
            end_state = self.start_state
        return cls(msg, end_state, self.tokeneater)
    def comment_state(self, msg):
        if msg == "\n":
            return self.start_state
        return self.comment_state
    def start_state(self, msg):
        if msg is None:
            return END
        if msg.isspace():
            return self.start_state
        if msg == '"':
            return self.submachine(StringMachine, msg)
        if msg == '$' or msg.isalpha():
            return self.submachine(IdentifierMachine, msg)
        if msg == '#':
            return self.comment_state
        if msg in SIMPLE_OPERATORS:
            self.tokeneater('OP', msg)
            return self.start_state
        if msg.isdigit() or msg in "-+":
            return self.submachine(NumberMachine, msg)
        raise Exception("Unexpected '%s'" % (msg,))

class SubLexMachine(object):
    def __init__(self, char, end_state, tokeneater):
        self.s = char
        self.end_state = end_state
        self.tokeneater = tokeneater
    def __call__(self, msg):
        return self.start_state(msg)

class StringMachine(SubLexMachine):
    def start_state(self, msg):
        dont_end(msg)
        if msg == '"':
            self.s += msg
            self.tokeneater('STR', self.s)
            return self.end_state
        if msg == '\\':
            return self.escape_state
        self.s += msg
        return self.start_state
    def escape_state(self, msg):
        dont_end(msg)
        if msg in ['"', '\\']:
            self.s += msg
            return self.start_state
        raise Exception("Unexpected escape char")

class IdentifierMachine(SubLexMachine):
    def start_state(self, msg):
        if msg is None or  msg.isspace() or msg in SIMPLE_OPERATORS:
            self.tokeneater('SYMBOL', self.s)
            return self.end_state(msg)
        if msg.isalnum() or msg in "_.":
            self.s += msg
            return self.start_state
        raise Exception("Unexpected '%s'" % (msg,))

class NumberMachine(SubLexMachine):
    def start_state(self, msg):
        if msg is None:
            self.tokeneater('INT', int(self.s))
            return self.end_state(msg)
        if msg.isdigit():
            self.s += msg
            return self.start_state
        if msg == ".":
            self.s += msg
            return self.decimal_state
        if msg in "Ee":
            raise Exception("Exponents not supported.")
        if msg.isspace() or msg in SIMPLE_OPERATORS:
            self.tokeneater('INT', int(self.s))
            return self.end_state(msg)
        raise Exception("Unexpected '%s'" % (msg,))
    def decimal_state(self, msg):
        if msg is None:
            self.tokeneater('INT', int(self.s))
            return self.end_state(msg)
        if msg.isdigit():
            self.s += msg
        if msg == "Ee":
            raise Exception("Exponents not supported.")
        if msg.isspace() or msg in SIMPLE_OPERATORS:
            self.tokeneater('FLOAT', float(self.s))
            return self.end_state(msg)
        raise Exception("Unexpected '%s'" % (msg,))

##########
# Parser #
##########

INTEGER_TYPES = ("ui1","ui2","ui4","i1","i2","i4","int")
DECIMAL_TYPES = ("r4","r8","number","fixed.14.4","float")
NUMBER_TYPES = INTEGER_TYPES + DECIMAL_TYPES
STRING_TYPES = ("string")
OTHER_TYPES = ("char","date","dateTime","dateTime.tz","time","time.tz","boolean","bin.base64","bin.hex","uri","uuid")

def expect(expected, actual):
    if expected != actual:
        raise Exception("Expected '%s', got '%s'." % (expected, actual))

def get_identifier(token):
    kind, value = token
    if kind != "SYMBOL":
        raise Exception("Expected identifier, got %s.", token)
    if value.startswith("$"):
        return "A_ARG_TYPE_" + value[1:]
    return value

def get_string(token):
    kind, value = token
    if kind != "STR":
        raise Exception("Expected string, got %s.", (token,))
    return value[1:-1]
    

def parse(line):
    xs = []
    def usetoken(kind, value):
        xs.append((kind, value))
    lexer = LexMachine(usetoken)
    lexer.run(line)
    xs.append(("EOL",""))
    return parsetokens(xs)

def parsetokens(tokens):
    tokens=iter(tokens)
    token = tokens.next()
    if token==("EOL",""):
        return None
    t1 = get_identifier(token)
    if t1=="var":
        return parsevar(tokens, evented=True)
    if t1=="action":
        return parseaction(tokens)
    if t1=="type":
        return parsevar(tokens, evented=False)
    raise Exception("Expected 'var', 'action' or 'type', got '%s'", (t1,))

def parsestatement(tokens):
    token = tokens.next()
    if token==("EOF",""):
        return None
    t1 = get_identifier(token)
    if t1=="var":
        return parsevar(tokens, evented=True)
    if t1=="action":
        return parseaction(tokens)
    if t1=="type":
        return parsevar(tokens, evented=False)
    raise Exception("Expected 'var', 'action' or 'type', got '%s'", (t1,))


def parsevar(tokens, evented):
    allowed_values = None
    allowed_range = None
    default_value = None
    identifier = get_identifier(tokens.next())
    expect(("OP",":"), tokens.next())
    vartype = get_identifier(tokens.next())
    while True:
        next_token = tokens.next()
        if next_token == EOS_TOKEN:
            break
        if next_token == ("OP","["):
            if vartype in STRING_TYPES:
                allowed_values = parseallowed(tokens)
            elif vartype in NUMBER_TYPES:
                allowed_range = parserange(tokens)
            else:
                raise Exception("Restrictions not allowed on type %s" % (vartype,))
            next_token = tokens.next()
            if next_token == EOS_TOKEN:
                break
        if next_token == ("OP","="):
            kind, value = tokens.next()
            if kind in ["STR", "INT", "FLOAT"]:
                default_value = value
            else:
                raise Exception("Expected string or number for default value, got %s." % ((kind, value),))
        next_token = tokens.next()
        if next_token!=EOS_TOKEN:
            raise Exception("Expected end of line, got %s", next_token)
        break
    return StateVar(identifier, vartype, evented, allowed_values, allowed_range, default_value)

def get_number(token):
    kind, value = token
    if kind not in ("INT", "FLOAT"):
        raise Exception("Expected number, got %s" % (token,))
    return value

def parseallowed(tokens):
    values = []
    values.append(get_string(tokens.next()))
    while True:
        next_token = tokens.next()
        if next_token == ("OP","]"):
            return values
        expect(("OP",","), next_token)
        next_token = tokens.next()
        values.append(get_string(next_token))

def parserange(tokens):
    low, high, step = None, None, None
    low = get_number(tokens.next())
    expect(("OP",":"), tokens.next())
    high = get_number(tokens.next())
    next_token = tokens.next()
    if next_token == ("OP", ":"):
        step = get_number(tokens.next())
        next_token = tokens.next()
    expect(("OP", "]"), next_token)
    return (low, high, step)

def parseaction(tokens):
    identifier = get_identifier(tokens.next())
    arguments = []
    expect(("OP","("), tokens.next())
    next_token = tokens.next()
    if next_token != ("OP", ")"):
        while True:
            argname = get_identifier(next_token)
            expect(("OP",":"), tokens.next())
            argdirection = get_identifier(tokens.next())
            if argdirection not in ("in", "out"):
                raise Exception("Expected 'in' or 'out', got '%s'." % (argdirection,))
            argtype = get_identifier(tokens.next())
            arguments.append(Parameter(argname, argdirection, argtype))
            next_token = tokens.next()
            if next_token == ("OP", ")"):
                break
            expect(("OP",","), next_token)
            next_token = tokens.next()
    next_token = tokens.next()
    if next_token == EOS_TOKEN:
        return Action(identifier, arguments)
    expect(("OP","="), next_token)
    argname = get_identifier(tokens.next())
    expect(("OP",":"), tokens.next())
    argtype = get_identifier(tokens.next())
    next_token = tokens.next()
    expect(EOS_TOKEN, next_token)
    retarg = Parameter(argname, "out", argtype, True)
    for i,arg in enumerate(arguments):
        if arg.direction=="out":
            arguments = arguments[:i] + retarg + arguments[i:]
            break
    else:
        arguments.append(retarg)
    return Action(identifier, arguments)

def parsefile(fileobj):
    xs = []
    def usetoken(kind, value):
        xs.append((kind, value))
    for line in fileobj:
        lexer = LexMachine(usetoken)
        lexer.run(line)
    xs.append(("EOF",""))

    variables = []
    actions = []
    fiter = iter(fileobj)
    tokeniter = iter(xs)
    while True:
        parsed = parsestatement(tokeniter)
        if parsed is None:
            break
        if isinstance(parsed, StateVar):
            variables.append(parsed)
        elif isinstance(parsed, Action):
            actions.append(parsed)
    return variables, actions

##############
# Data model #
##############

class StateVar(object):
    def __init__(self, name, vartype, evented=True, allowed_values=None, allowed_range=None, default_value=None):
        self.name = name
        self.vartype = vartype
        self.evented = evented
        self.allowed_values = allowed_values
        self.allowed_range = allowed_range
        self.default_value = default_value
    def __repr__(self):
        return "StateVar(%s, %s, evented=%s, allowed_values=%s, allowed_range=%s, default_value=%s)"%(self.name, self.vartype, self.evented, self.allowed_values, self.allowed_range, self.default_value)

class Action(object):
    def __init__(self, name, parameters):
        self.name = name
        self.parameters = parameters
    def __repr__(self):
        return "Action(%s, %s)" % (self.name, self.parameters)

class Parameter(object):
    def __init__(self, name, direction, argtype, retval=False):
        self.name = name
        self.direction = direction
        self.argtype = argtype
        self.retval = retval
    def __repr__(self):
        return "Parameter(%s, %s, %s, %s)" % (self.name, self.direction, self.argtype, self.retval)

def print_scpd(variables, actions, outfile = None):
    if outfile is None:
        outfile = sys.stdout
    outfile.write("<scpd>\n")
    outfile.write("    <serviceStateTable>\n")
    for v in variables:
        outfile.write("        <stateVariable>\n")
        outfile.write("            <name>%s</name>\n" % v.name)
        outfile.write("            <sendEventsAttribute>%s</sendEventsAttribute>\n" % ("yes" if v.evented else "no"))
        outfile.write("            <dataType>%s</dataType>\n" % v.vartype)
        if v.default_value is not None:
            outfile.write("            <defaultValue>%s</defaultValue>\n" % v.default_value)
        if v.allowed_values is not None:
            outfile.write("            <allowedValueList>\n")
            for av in v.allowed_values:
                outfile.write("                <allowedValue>%s</allowedValue>\n" % av)
            outfile.write("            </allowedValueList>\n")
        if v.allowed_range is not None:
            outfile.write("            <allowedValueRange>\n")
            outfile.write("                <minimum>%s</minimum>\n" % v.allowed_range[0])
            outfile.write("                <maximum>%s</maximum>\n" % v.allowed_range[1])
            if v.allowed_range[2] is not None:
                outfile.write("                <step>%s</step>\n" % v.allowed_range[2])
            outfile.write("            </allowedValueRange>\n")
        outfile.write("        </stateVariable>\n")
    outfile.write("    </serviceStateTable>\n")
    outfile.write("    <actionList>\n")
    for a in actions:
        outfile.write("        <action>\n")
        outfile.write("            <name>%s</name>\n" % a.name)
        if len(a.parameters) > 0:
            outfile.write("            <argumentList>\n")
            for arg in a.parameters:
                outfile.write("                <argument>\n")
                outfile.write("                    <name>%s</name>\n" % arg.name)
                outfile.write("                    <direction>%s</direction>\n" % arg.direction)
                outfile.write("                    <relatedStateVariable>%s</relatedStateVariable>\n" % arg.argtype)
                outfile.write("                </argument>\n")
            outfile.write("            </argumentList>\n")
        outfile.write("        </action>\n")
    outfile.write("    </actionList>\n")
    outfile.write("</scpd>\n")

def parse_args():
    usage = (
        "\n"+
        "    %prog [options]\n"+
        "\n"+
        "Convert a uSCPD file to SCPD XML.")
    parser = OptionParser(usage=usage)
    parser.add_option("-i", "--input", dest="input", action="store", default=None, help="Input uSCPD file. (Default = stdin)")
    parser.add_option("-o", "--output", dest="output", action="store", default=None, help="Output XML file. (Default = stdout)")
    parser.set_defaults(indent="", endings="")
    return parser.parse_args()

def main():
    '''
    Parse uscpd file from stdin,
    Write SCPD XML file to stdout.
    '''
    options, args = parse_args()
    if len(args)>0:
        print "Usage:"
        print "    uscpd2xml < input.uscpd > output.xml"
        sys.exit(1)

    infile = sys.stdin if options.input is None else file(options.input, 'r')
    outfile = sys.stdout if options.output is None else file(options.output, 'w')
    a,b = parsefile(infile)
    print_scpd(a,b,outfile)

if __name__=="__main__":
    main()
