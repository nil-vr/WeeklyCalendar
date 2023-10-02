use std::{
    env,
    fs::{self, File},
    io::BufWriter,
    path::PathBuf,
};

use flate2::{write::GzEncoder, Compression};
use tar::HeaderMode;

fn main() {
    bundle_web();
}

fn bundle_web() {
    println!("cargo:rerun-if-changed=../web/dist");

    let mut out_path = PathBuf::from(env::var("OUT_DIR").unwrap());
    out_path.push("web.tgz");
    let mut archive = tar::Builder::new(GzEncoder::new(
        BufWriter::new(File::create(&out_path).unwrap()),
        Compression::best(),
    ));
    archive.mode(HeaderMode::Deterministic);

    for entry in fs::read_dir("../web/dist")
        .expect("../web/dist does not exist (run `trunk build` in ../web)")
    {
        let entry = entry.unwrap();
        let file_type = entry.file_type().unwrap();
        if file_type.is_dir() {
            archive
                .append_dir_all(entry.file_name(), entry.path())
                .unwrap();
        } else {
            archive
                .append_path_with_name(entry.path(), entry.file_name())
                .unwrap();
        }
    }
    archive.finish().unwrap();
}
