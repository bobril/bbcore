class MediaRuleBuilder {
    tokens = [];
    pushOptionalTokens(behaviour, mediaType) {
        !!behaviour && this.tokens.push({
            type: behaviour
        });
        !!mediaType && this.tokens.push({
            type: mediaType
        });
    }
    rule(behaviour, mediaType = "all") {
        this.pushOptionalTokens(behaviour, mediaType);
        return this;
    }
    and(mediaRule) {
        this.tokens.push({
            type: "and"
        });
        this.tokens.push(mediaRule);
        return this;
    }
    or() {
        this.tokens.push({
            type: "or"
        });
        return this;
    }
    build() {
        return this.tokens.reduce(toRule, "");
    }
}

function toRule(buffer, token) {
    let str = "";
    switch (token.type) {
      case "aspect-ratio":
        str = `(${token.type}: ${token.width}/${token.height})`;
        break;

      case "all":
      case "and":
      case "not":
      case "only":
      case "print":
      case "screen":
      case "speech":
        str = `${token.type}`;
        break;

      case "or":
        str = ",";
        break;

      case "color":
        str = `(${token.type})`;
        break;

      case "max-height":
      case "max-width":
      case "min-height":
      case "min-width":
        str = `(${token.type}: ${token.value}${token.unit})`;
        break;

      case "min-color":
      case "orientation":
        str = `(${token.type}: ${token.value})`;
        break;

      default:
        str = emptyQuery(token);
    }
    return buffer + str + " ";
}

function emptyQuery(_token) {
    return "";
}

export function createMediaQuery() {
    return new MediaRuleBuilder();
}

