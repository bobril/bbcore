import type { User, UserId } from "./types";
import { createUser, type UserOptions } from "./factory";

export function build(id: UserId, options: UserOptions): User {
    return createUser(id, options);
}
