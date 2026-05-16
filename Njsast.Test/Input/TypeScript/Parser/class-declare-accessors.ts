class Service {
    declare get value(): string;
    declare get "x-y"(): string;
    declare get [dynamicName](): string;
    declare get #secret(): string;
    declare static set cached(value: string);
    declare field: string;
    ready = true;
}
