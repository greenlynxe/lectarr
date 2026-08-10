# Metadata proxy for lectarr: upstream rreading-glasses (pinned) with the
# lectarr patch applied — paginated Goodreads editions and translated
# editions re-attributed to their canonical author, so French (and other
# non-English) editions actually reach the app.
#
# The patch is also being proposed upstream; once merged, this image can be
# replaced with blampe/rreading-glasses:latest.

FROM golang:1.26-alpine AS build

RUN apk add --no-cache git

WORKDIR /app

# Pin the upstream commit the patch was written against.
ARG UPSTREAM_REPO=https://github.com/blampe/rreading-glasses.git
ARG UPSTREAM_COMMIT=a2939b625d91389d3f0a3e58cbc3bfa7ebb8390a

RUN git clone ${UPSTREAM_REPO} . && git checkout ${UPSTREAM_COMMIT}

COPY 0001-feat-gr-surface-translated-editions.patch /tmp/
RUN git -c user.name=lectarr -c user.email=build@lectarr.local am /tmp/0001-feat-gr-surface-translated-editions.patch

RUN CGO_ENABLED=0 go build -o /app/main -ldflags="-w -s" ./cmd/rggr

FROM gcr.io/distroless/static:nonroot AS app
COPY --from=build /app/main /main
COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/

EXPOSE 8788
CMD ["/main"]
