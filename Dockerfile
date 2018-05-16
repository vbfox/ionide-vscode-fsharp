FROM vbfox/fable-build:aspnet-2.0.8-2.1.200-stretch-mono-5.10.0.160-yarn-1.6.0

WORKDIR /build

# Package lock files are copied independently and their respective package
# manager are executed after.
#
# This is voluntary as docker will cache images and only re-create them if
# the already-copied files have changed, by doing that as long as no package
# is installed or updated we keep the cached container and don't need to
# re-download.

# Initialize node_modules
COPY yarn.lock package.json ./
RUN yarn install
RUN npm install -g vsce

# Initialize paket packages
COPY paket.dependencies paket.lock paket.exe ./
RUN mkdir src
COPY src/*.fsproj src/paket.references ./src/
COPY .paket .paket
RUN mono paket.exe restore && cd src && dotnet restore && cd ..

# Copy everything else and run the build
COPY . ./

VOLUME [ "/build/temp", "/build/release" ]

ENTRYPOINT ["./build.sh"]
CMD ["BuildPackage"]