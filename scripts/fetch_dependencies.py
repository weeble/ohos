#!/bin/env python

# This script is specific to Linn's network environment. It copies the
# project's dependencies from a known location on the network into the
# dependencies folder. After running this, you should be able to run
# "waf configure" and it should detect all the dependencies.

# If you are building this project without access to the Linn network,
# you are responsible for building or downloading the dependencies and
# either placing them in the dependencies folder or specifying their
# locations elsewhere when you invoke "waf configure".

# usage:
#   fetch_dependencies.py --target=[Windows-x86 | Windows-x64 | Linux-x86 | Linux-x64 | Linux-ARM]
# or omit the target to choose the default for your host platform.

import build

def main():
    builder = build.Build()
    builder.prebuild()
    builder.copy_dependencies()

if __name__ == "__main__":
    main()
