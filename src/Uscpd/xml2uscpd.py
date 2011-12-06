from xml.etree.ElementTree import parse
import sys

description = "Translate XML files to uSCPD."
command_group = "Developer tools"

INTEGER_TYPES = ("ui1","ui2","ui4","i1","i2","i4","int")
DECIMAL_TYPES = ("r4","r8","number","fixed.14.4","float")
NUMBER_TYPES = INTEGER_TYPES + DECIMAL_TYPES
STRING_TYPES = ("string")
OTHER_TYPES = ("char","date","dateTime","dateTime.tz","time","time.tz","boolean","bin.base64","bin.hex","uri","uuid")

def range_to_text(r):
    if r is None:
        return None
    minimum, maximum, step = r
    if step is None:
        return "[%s:%s]" % (minimum, maximum)
    return "[%s:%s:%s]" % (minimum, maximum, step)

def stringify(s):
    if s is None:
        s = ""
    return '"%s"' % (s.replace("\\", "\\\\").replace('"', '\\"'),)

def values_to_text(valuelist):
    if valuelist is None:
        return None
    return "[%s]" % (", ".join(stringify(v) for v in valuelist))

def value_to_literal(value, datatype):
    if datatype in NUMBER_TYPES:
        return value
    return stringify(value)

def value_to_identifier(value):
    if value.startswith("A_ARG_TYPE_"):
        return "$"+value[len("A_ARG_TYPE_"):]
    return value

def transform(infile, outfile):
    etree = parse(infile)
    state_vars = etree.find("serviceStateTable")
    for var in state_vars:
        name = var.findtext("name")
        evented = var.findtext("sendEventsAttribute")
        datatype = var.findtext("dataType")
        allowedvaluelist = var.find("allowedValueList")
        if allowedvaluelist is not None:
            allowedvalues = list(v.text for v in allowedvaluelist)
        else:
            allowedvalues = None
        allowedvaluerange = var.find("allowedValueRange")
        if allowedvaluerange is not None:
            allowedrange = (
                    allowedvaluerange.findtext("minimum"),
                    allowedvaluerange.findtext("maximum"),
                    allowedvaluerange.findtext("step"))
        else:
            allowedrange = None
        defaultvalue = var.findtext("defaultValue")

        tag = "var" if evented=="yes" else "type"
        identifier = value_to_identifier(name)
        rangetext = range_to_text(allowedrange)
        valuestext = values_to_text(allowedvalues)
        defaulttext = (
                None if defaultvalue is None else
                "= " + value_to_literal(defaultvalue, datatype))
        tokens = [tag, identifier, ":", datatype, rangetext, valuestext, defaulttext]
        outfile.write(" ".join(t for t in tokens if t is not None) + ";\n")

    actions = etree.find("actionList")
    for action in actions:
        name = action.findtext("name")
        argumentlist = action.find("argumentList")
        if argumentlist is None:
            argumentlist = []
        argstrings = []
        retargstring = ""
        for a in argumentlist:
            argname = a.findtext("name")
            argdirection = a.findtext("direction")
            argvar = a.findtext("relatedStateVariable")
            argretval = a.find("retval") is not None
            argstring = "%s : %s %s" % (value_to_identifier(argname), argdirection, value_to_identifier(argvar))
            if argretval:
                retargstring = "= " + argstring
            else:
                argstrings.append(argstring)
        outfile.write("action %s(%s)%s;\n" % (
                name,
                ", ".join(argstrings),
                retargstring))

def main():
    '''
    Parse SCPD XML file from stdin,
    Write uscpd file to stdout.
    '''
    if len(sys.argv)>=2:
        print "Usage:"
        print "    xml2uscpd2 < input.xml > output.uscpd"
        sys.exit(0)
    transform(sys.stdin, sys.stdout)

if __name__=="__main__":
    main()
