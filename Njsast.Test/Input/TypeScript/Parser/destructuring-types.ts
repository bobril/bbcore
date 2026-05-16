const { name, age }: { name: string; age: number } = person;
const [first, second]: [string, number] = tuple;
function log({ title, body }: { title: string; body: string }): void {
    console.log(title, body);
}
