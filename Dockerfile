FROM microsoft/aspnetcore-build:2.0.8-2.1.200-stretch AS builder

# Install Mono
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
ENV MONO_VERSION 5.10.0.160
RUN echo "deb http://download.mono-project.com/repo/debian stable-jessie/snapshots/$MONO_VERSION main" > /etc/apt/sources.list.d/mono-official-stable.list \
  && apt-get update \
  && apt-get install -y mono-runtime binutils curl mono-devel ca-certificates-mono fsharp mono-vbnc nuget referenceassemblies-pcl \
  && rm -rf /var/lib/apt/lists/* /tmp/*

# Install Yarn
ENV YARN_VERSION 1.6.0
RUN set -ex \
  && for key in \
    E21930C4D0A4AA4618581F7AE074D16EB6FF4DE3 \
  ; do \
    gpg --keyserver pgp.mit.edu --recv-keys "$key" || \
    gpg --keyserver keyserver.pgp.com --recv-keys "$key" || \
    gpg --keyserver ha.pool.sks-keyservers.net --recv-keys "$key" ; \
  done \
  && curl -fSLO --compressed "https://yarnpkg.com/downloads/$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz" \
  && curl -fSLO --compressed "https://yarnpkg.com/downloads/$YARN_VERSION/yarn-v$YARN_VERSION.tar.gz.asc" \
  && gpg --batch --verify yarn-v$YARN_VERSION.tar.gz.asc yarn-v$YARN_VERSION.tar.gz \
  && mkdir -p /opt/yarn \
  && tar -xzf yarn-v$YARN_VERSION.tar.gz -C /opt/yarn --strip-components=1 \
  && ln -s /opt/yarn/bin/yarn /usr/local/bin/yarn \
  && ln -s /opt/yarn/bin/yarn /usr/local/bin/yarnpkg \
  && rm yarn-v$YARN_VERSION.tar.gz.asc yarn-v$YARN_VERSION.tar.gz

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

# Initialize paket packages
COPY paket.dependencies paket.lock paket.exe ./
RUN mkdir src
COPY src/*.fsproj src/paket.references ./src/
COPY .paket .paket
RUN mono paket.exe restore && cd src && dotnet restore && cd ..

# Copy everything else and run the build
COPY . ./
RUN ./build.sh build