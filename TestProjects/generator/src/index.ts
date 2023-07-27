export function getDescription(): ScriptDescription {
  return {
    description: "Imports clients from JSON data source.",
    input: [
      {
        id: "inputData",
        type: "InputResource",
        description: "Input Data",
        displayName: "Input Data",
        required: true,
      },
      {
        id: "database",
        type: "Connector",
        displayName: "Database",
        description: "Database",
        required: true,
      },
    ],
  };
}

export async function execute(context: Context): Promise<void> {
  for await (const val of new ClassGen().foo()) {
    console.log(123);
  }
  for await (const val of foo()) {
    console.log(123);
  }
}

class ClassGen {
  async *foo() {
    yield await Promise.resolve("a");
    yield await Promise.resolve("b");
    yield await Promise.resolve("c");
  }
}

async function* foo() {
  yield await Promise.resolve("a");
  yield await Promise.resolve("b");
  yield await Promise.resolve("c");
}
