##########################################################################
#  Copyright 2015-2017 Intel Corporation All Rights Reserved.
#
#  The source code, information and material ("Material") contained
#  herein is owned by Intel Corporation or its suppliers or licensors,
#  and title to such Material remains with Intel Corporation or its
#  suppliers or licensors. The Material contains proprietary information
#  of Intel or its suppliers and licensors. The Material is protected by
#  worldwide copyright laws and treaty provisions. No part of the
#  Material may be used, copied, reproduced, modified, published,
#  uploaded, posted, transmitted, distributed or disclosed in any way
#  without Intel's prior express written permission.
#
#  No license under any patent, copyright or other intellectual property
#  rights in the Material is granted to or conferred upon you, either
#  expressly, by implication, inducement, estoppel or otherwise. Any
#  license under such intellectual property rights must be express and
#  approved by Intel in writing.
#
#  Unless otherwise agreed by Intel in writing, you may not remove or
#  alter this notice or any other notice embedded in Materials by Intel
#  or Intel's suppliers or licensors in any way.
##########################################################################
import sys
if sys.version_info[0] == 3:
    def to_bytes(s):
        if isinstance(s, str):
            return bytes(s, 'ascii')
        return s
    binary_stdout = sys.stdout.buffer
else:
    def to_bytes(s):
        return s
    binary_stdout = sys.stdout

DEBUGGING = False

def readin():
    return sys.stdin.readline().strip()

def writeout(text, doFlush = True):
    binary_stdout.write(text)
    binary_stdout.flush()
    if doFlush:
        binary_stdout.flush()

def readin_dbg():
    line = sys.stdin.readline().strip()
    with open(sys.argv[2], 'ab') as dbg:
        dbg.write(to_bytes(line + '\n'))
    return line

def writeout_dbg(text, doFlush = True):
    with open(sys.argv[1], 'ab') as dbg:
        dbg.write(to_bytes(text))
    binary_stdout.write(text)
    binary_stdout.flush()
    if doFlush:
        binary_stdout.flush()

def debug_excepthook(excType, excValue, excTrace):
    try:
        import linecache
        def getline(frame):
            return linecache.getline(frame.f_code.co_filename, frame.f_lineno).strip()
    except:
        def getline(frame):
            return ''

    result, locals = ['', ''], []
    trace, frame = excTrace, None
    try:
        while trace:
            frame = trace.tb_frame
            trace = trace.tb_next
            result.append('File "%s", line %s, in %s' % (frame.f_code.co_filename,
                    frame.f_lineno, frame.f_code.co_name))
            src_line = getline(frame)
            if src_line:
                result.append('    %s' % src_line)
        for localName, localValue in frame.f_locals.items():
            try:
                localDisplay = 'value: ' + repr(localValue)[:100]
            except:
                localDisplay = 'exception <%s:%s>' % tuple(sys.exc_info()[:2])
            locals.append('%s <%s> %s' % (localName, type(localValue), localDisplay))
    finally:
        del trace, frame
    result.append('%s: %s' % (excType, excValue))
    result.extend(['-' * 20, 'Locals:'])
    result.extend(locals)
    with open(sys.argv[1], 'ab') as dbg:
        dbg.write(to_bytes('\n'.join(result)))

def setEnv():
    global struct
    if 'site' in sys.modules:
        # already initialized; most likely interactive testing mode
        return
    sep, newSysPath, newExecPrefix, newPrefix = [readin() for i in range(4)]
    if sep:
        sys.path.extend(newSysPath.split(sep)[:-1])
        sys.prefix = newPrefix
        sys.exec_prefix = newExecPrefix
    import struct
    try:
        import site
        import os
        sys.path.append(os.path.abspath(os.path.dirname(__file__)))
        writeout(struct.pack('@i', 1))
    except:
        if DEBUGGING:
            debug_excepthook(*sys.exc_info())
        writeout(struct.pack('@i', 0))

def main():
    from parsepy import getModuleCallables, CodeObjectType
    import os

    codeSep = {CodeObjectType.Function: '@'}
    def getCodePathStr(codePath):
        view = []
        for codeType, codeName in codePath:
            view.extend([codeName, codeSep.get(codeType, '.')])
        if (len(codePath) > 1) and (codePath[0][0] == CodeObjectType.Module):
            return ''.join(view[2:-1]) # skip first module item and last separator
        else:
            return ''.join(view[:-1]) # skip last separator only

    while True:
        filePath = readin()
        if filePath:
            if not os.path.exists(filePath):
                writeout(struct.pack('@i', 0))
                continue
            try:
                calls = getModuleCallables(filePath, False)
            except:
                writeout(struct.pack('@i', -1))
                if DEBUGGING:
                    debug_excepthook(*sys.exc_info())
                continue
            writeout(struct.pack('@i', len(calls)), False)
            for codeStart, codeStop, codePath, codeArgStr, codeHash in calls:
                codeStr = to_bytes(getCodePathStr(codePath) + codeArgStr)
                codeNameStr = to_bytes(codePath[-1][1])
                writeout(struct.pack('@iii{0}s{1}s'.format(len(codeNameStr), len(codeStr)),
                         codeStart, len(codeNameStr), len(codeStr), codeNameStr, codeStr), False)
            binary_stdout.flush()
        else:
            writeout(struct.pack('@i', 0))
            return

if __name__ == '__main__':
    if len(sys.argv) > 1:
        writeout = writeout_dbg
        DEBUGGING = True
        sys.excepthook = debug_excepthook
        with open(sys.argv[1], 'w'):
            pass
    if len(sys.argv) > 2:
        readin = readin_dbg
        with open(sys.argv[2], 'w'):
            pass
    setEnv()
    if sys.platform == 'win32':
        import msvcrt, os
        msvcrt.setmode(sys.stdout.fileno(), os.O_BINARY)
    else:
        pass # this works on Linux without separate controlling
    # for debugging uncomment the next line
    #from ppy_dbg import setEnvDebug; setEnvDebug()
    main()
