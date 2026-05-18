{
  const promises = [Promise.resolve(0)] as const

  Promise.all(promises).then((results) => {
    const first = results[0]
    const second = results[1]
  })
}
