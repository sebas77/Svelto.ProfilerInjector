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
import collections
import inspect

if sys.version_info[0] == 3:
    def cast_lnotab_char(ch):
        return ch
    def cast_to_bytes(s):
        return bytes(s, 'ascii', 'ignore')
else:
    cast_lnotab_char = ord
    def cast_to_bytes(s):
        return s

def getCodeRange(code):
    return code.co_firstlineno, \
           code.co_firstlineno + sum([cast_lnotab_char(ch) for ch in code.co_lnotab[1::2]])

# constants from "opcode.h"
STORE_LOAD_INSTR_LEN = 3
BUILD_CLASS_OPCODE_LEN = 1

def getNestedCodeString(code, subcode):
    codeStart, codeStop = None, None
    if isinstance(subcode, int):
        subcode = code.co_consts[subcode]
    subStart, subStop = getCodeRange(subcode)
    codePos, linePos = 0, code.co_firstlineno
    ltab = code.co_lnotab
    for posDiff, lineDiff in zip(*[iter(ltab)]*2):
        codePos += cast_lnotab_char(posDiff)
        linePos += cast_lnotab_char(lineDiff)
        if codeStart is None:
            if linePos >= subStart:
                codeStart = codePos
        elif linePos >= subStop:
            codeStop = codePos
            break
    if codeStart is None:
        codeStart = 0
        #raise ValueError('Cannot find subcode generating code')
    if codeStop is None:
        codeStop = codePos
    return code.co_code[codeStart:codeStop - STORE_LOAD_INSTR_LEN]

def funcWithNestedClass():
    class C:
        pass
    pass
BUILD_CLASS_OPCODE = getNestedCodeString(funcWithNestedClass.__code__,
                                     [c for c in funcWithNestedClass.__code__.co_consts if inspect.iscode(c)][0]) \
                         [-BUILD_CLASS_OPCODE_LEN:]

def isNestedClass(code, subcode):
    return getNestedCodeString(code, subcode).endswith(BUILD_CLASS_OPCODE)


class CodeObjectType:
    Module, Function, Method, Class, Unknown = range(5)

CodePathElement = collections.namedtuple('CodePathElement', 'type name')
def getCodeChildren(codeObject, codeType = CodeObjectType.Unknown, parentRoot = None):
    if not parentRoot:
        parentRoot = []
    rootPath = parentRoot + [CodePathElement(codeType, codeObject.co_name)]
    result = [(rootPath, codeObject)]
    for obj in codeObject.co_consts:
        # walking nested code objects
        if inspect.iscode(obj):
            subType = CodeObjectType.Class if isNestedClass(codeObject, obj) else CodeObjectType.Function
            result.extend(getCodeChildren(obj, subType, rootPath))
    return result

def getCodeArgsStr(codeType, code):
    if codeType in (CodeObjectType.Function, CodeObjectType.Method):
        args, varargs, varkw = inspect.getargs(code)
        return inspect.formatargspec(args, varargs, varkw)
    else:
        return ''

def getModuleCallables(fileName, asDict = True):
    with open(fileName, 'rUb') as input:
        # * adding ending newline to make compile() happy on pre-2.7 Python
        # * adding "dummydummy = None" so that module won't end with class declaration
        #      because if it does getNestedCodeString() isn't able to extract code string
        #      that builds the declaration as it relies on line numbers to find
        #      the position in the code string, and if module has no body its last line
        #      in code object would be the one declaring class
        code = compile(input.read() + cast_to_bytes('\n#dummy code\ndummydummy = None\n'), fileName, 'exec')
    analyzed = getCodeChildren(code, CodeObjectType.Module)
    if asDict:
        result = collections.defaultdict(list)
        for codePath, codeObj in analyzed:
            result[codeObj.co_firstlineno].append(getCodeRange(codeObj) + (codePath,
                                                   getCodeArgsStr(codePath[-1].type, codeObj),
                                                   hash(codeObj)))
        return dict(result)
    else:
        return [(getCodeRange(codeObj) + (codePath, getCodeArgsStr(codePath[-1].type, codeObj), hash(codeObj))) for (codePath, codeObj) in analyzed]

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print('Usage: %s <fileName>' % sys.argv[0])
        sys.exit(1)

    testLambdas = (lambda: 1, lambda bb: 2, lambda *args: 3)

    allCallables = getModuleCallables(sys.argv[1])
    for line  in sorted(allCallables.keys()):
        print('%d:' % line)
        for (codeStart, codeStop, codePath, codeArgs, codeHash) in allCallables[line]:
            codePathView = []
            for (codeType, codeName) in codePath:
                if codeType == CodeObjectType.Class:
                    codePathView.extend([codeName, '.'])
                elif codeType == CodeObjectType.Function:
                    codePathView.extend([codeName, '::'])
                elif codeType == CodeObjectType.Module:
                    codePathView.extend([codeName, '.'])
            print('    %s%s  [%X] %d-%d' % (''.join(codePathView[:-1]), codeArgs, codeHash, codeStart, codeStop))
