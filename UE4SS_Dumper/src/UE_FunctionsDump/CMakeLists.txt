cmake_minimum_required(VERSION 3.22)

set(TARGET UE_FunctionsDump)
project(${TARGET})

add_library(${TARGET} SHARED "src/dllmain.cpp")
target_include_directories(${TARGET} PRIVATE .)

target_link_libraries(${TARGET} PRIVATE UE4SS)
